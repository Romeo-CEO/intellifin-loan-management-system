using System.Diagnostics;
using IntelliFin.IdentityService.Models;
using IntelliFin.Shared.DomainModels.Data;
using IntelliFin.Shared.DomainModels.Entities;
using IntelliFin.Shared.DomainModels.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using AuditEvent = IntelliFin.IdentityService.Models.AuditEvent;
namespace IntelliFin.IdentityService.Services;

public class SoDValidationService : ISoDValidationService
{
    private readonly LmsDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly ILogger<SoDValidationService> _logger;

    public SoDValidationService(
        LmsDbContext dbContext,
        IAuditService auditService,
        ILogger<SoDValidationService> logger)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<SoDValidationResult> ValidateRoleAssignmentAsync(string userId, string roleId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(roleId);

        var stopwatch = Stopwatch.StartNew();

        var user = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Id, u.Username })
            .FirstOrDefaultAsync(ct);

        if (user is null)
        {
            throw new KeyNotFoundException($"User {userId} not found");
        }

        var role = await _dbContext.Roles
            .AsNoTracking()
            .Include(r => r.RolePermissions.Where(rp => rp.IsActive))
                .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == roleId, ct);

        if (role is null)
        {
            throw new KeyNotFoundException($"Role {roleId} not found");
        }

        var permissionSet = await BuildUserPermissionSetAsync(userId, ct);
        var candidatePermissions = ExtractActivePermissionNames(role);

        foreach (var permission in candidatePermissions)
        {
            permissionSet.Add(permission);
        }

        var rules = await GetActiveRulesAsync(ct);
        var conflicts = EvaluateConflicts(rules, permissionSet, candidatePermissions, role.Id, role.Name);

        if (conflicts.Count > 0)
        {
            await LogConflictsAsync(user.Id, user.Username, role.Id, role.Name, conflicts, ct);
        }

        stopwatch.Stop();
        _logger.LogInformation(
            "SoD validation for role assignment user={UserId} role={RoleId} completed in {Elapsed} ms with {Conflicts} conflict(s)",
            userId,
            roleId,
            stopwatch.Elapsed.TotalMilliseconds,
            conflicts.Count);

        return new SoDValidationResult
        {
            Conflicts = conflicts,
            IsAllowed = conflicts.All(c => c.Enforcement != SoDEnforcementLevel.Strict)
        };
    }

    public async Task<SoDValidationResult> ValidatePermissionConflictsAsync(string userId, string[] newPermissions, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var stopwatch = Stopwatch.StartNew();
        var permissionSet = await BuildUserPermissionSetAsync(userId, ct);
        var candidatePermissions = new HashSet<string>(newPermissions ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);

        foreach (var permission in candidatePermissions)
        {
            permissionSet.Add(permission);
        }

        var rules = await GetActiveRulesAsync(ct);
        var conflicts = EvaluateConflicts(rules, permissionSet, candidatePermissions, null, null);

        if (conflicts.Count > 0)
        {
            await LogConflictsAsync(userId, null, null, null, conflicts, ct);
        }

        stopwatch.Stop();
        _logger.LogInformation(
            "SoD permission validation for user={UserId} completed in {Elapsed} ms with {Conflicts} conflict(s)",
            userId,
            stopwatch.Elapsed.TotalMilliseconds,
            conflicts.Count);

        return new SoDValidationResult
        {
            Conflicts = conflicts,
            IsAllowed = conflicts.All(c => c.Enforcement != SoDEnforcementLevel.Strict)
        };
    }

    public async Task<SoDViolationReport> DetectViolationsAsync(CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var rules = await GetActiveRulesAsync(ct);
        if (rules.Count == 0)
        {
            return new SoDViolationReport
            {
                GeneratedAtUtc = DateTime.UtcNow,
                Violations = Array.Empty<SoDViolation>()
            };
        }

        var users = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.IsActive)
            .Include(u => u.UserRoles.Where(ur => ur.IsActive))
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RolePermissions.Where(rp => rp.IsActive))
                        .ThenInclude(rp => rp.Permission)
            .ToListAsync(ct);

        var violations = new List<SoDViolation>();

        foreach (var user in users)
        {
            var permissionSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var roleNames = new List<string>();

            foreach (var assignment in user.UserRoles.Where(ur => ur.IsActive && ur.Role != null && ur.Role.IsActive))
            {
                roleNames.Add(assignment.Role.Name);
                foreach (var rolePermission in assignment.Role.RolePermissions.Where(rp => rp.IsActive && rp.Permission.IsActive))
                {
                    permissionSet.Add(rolePermission.Permission.Name);
                }
            }

            if (permissionSet.Count == 0)
            {
                continue;
            }

            var conflicts = EvaluateConflicts(rules, permissionSet, permissionSet, null, null);
            if (conflicts.Count == 0)
            {
                continue;
            }

            await LogConflictsAsync(user.Id, user.Username, null, null, conflicts, ct);

            foreach (var conflict in conflicts)
            {
                violations.Add(new SoDViolation
                {
                    UserId = user.Id,
                    Username = user.Username,
                    RuleId = conflict.RuleId,
                    RuleName = conflict.RuleName,
                    Enforcement = conflict.Enforcement,
                    ConflictingPermissions = conflict.ConflictingPermissions,
                    TriggeringPermissions = conflict.TriggeringPermissions,
                    Roles = roleNames.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
                });
            }
        }

        stopwatch.Stop();
        _logger.LogInformation(
            "SoD violation scan completed in {Elapsed} ms with {ViolationCount} violation(s)",
            stopwatch.Elapsed.TotalMilliseconds,
            violations.Count);

        return new SoDViolationReport
        {
            GeneratedAtUtc = DateTime.UtcNow,
            Violations = violations
        };
    }

    private async Task<HashSet<string>> BuildUserPermissionSetAsync(string userId, CancellationToken ct)
    {
        var assignments = await _dbContext.UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == userId && ur.IsActive)
            .Include(ur => ur.Role)
                .ThenInclude(r => r.RolePermissions.Where(rp => rp.IsActive))
                    .ThenInclude(rp => rp.Permission)
            .ToListAsync(ct);

        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var assignment in assignments)
        {
            if (assignment.Role is null || !assignment.Role.IsActive)
            {
                continue;
            }

            foreach (var rolePermission in assignment.Role.RolePermissions.Where(rp => rp.IsActive && rp.Permission.IsActive))
            {
                permissions.Add(rolePermission.Permission.Name);
            }
        }

        return permissions;
    }

    private static HashSet<string> ExtractActivePermissionNames(Role role)
    {
        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rolePermission in role.RolePermissions.Where(rp => rp.IsActive && rp.Permission.IsActive))
        {
            permissions.Add(rolePermission.Permission.Name);
        }

        return permissions;
    }

    private async Task<List<SoDRule>> GetActiveRulesAsync(CancellationToken ct)
    {
        return await _dbContext.SoDRules
            .AsNoTracking()
            .Where(r => r.IsActive)
            .ToListAsync(ct);
    }

    private static List<SoDConflict> EvaluateConflicts(
        IReadOnlyCollection<SoDRule> rules,
        HashSet<string> permissionSet,
        HashSet<string> candidatePermissions,
        string? attemptedRoleId,
        string? attemptedRoleName)
    {
        var conflicts = new List<SoDConflict>();

        foreach (var rule in rules)
        {
            var normalizedPermissions = rule.ConflictingPermissions
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (normalizedPermissions.Length == 0)
            {
                continue;
            }

            if (normalizedPermissions.All(permissionSet.Contains))
            {
                var triggeringPermissions = normalizedPermissions
                    .Where(candidatePermissions.Contains)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                if (triggeringPermissions.Length == 0)
                {
                    triggeringPermissions = normalizedPermissions;
                }

                conflicts.Add(new SoDConflict
                {
                    RuleId = rule.Id,
                    RuleName = rule.Name,
                    Enforcement = rule.Enforcement,
                    ConflictingPermissions = normalizedPermissions,
                    TriggeringPermissions = triggeringPermissions,
                    AttemptedRoleId = attemptedRoleId,
                    AttemptedRoleName = attemptedRoleName
                });
            }
        }

        return conflicts;
    }

    private async Task LogConflictsAsync(
        string userId,
        string? username,
        string? roleId,
        string? roleName,
        IReadOnlyCollection<SoDConflict> conflicts,
        CancellationToken ct)
    {
        foreach (var conflict in conflicts)
        {
            var auditEvent = new AuditEvent
            {
                ActorId = username ?? "system",
                Action = "SoDViolation",
                Entity = "UserRole",
                EntityId = !string.IsNullOrWhiteSpace(roleId) ? $"{userId}:{roleId}" : userId,
                Timestamp = DateTime.UtcNow,
                Details = new Dictionary<string, object>
                {
                    ["userId"] = userId,
                    ["username"] = username ?? string.Empty,
                    ["attemptedRoleId"] = roleId ?? string.Empty,
                    ["attemptedRoleName"] = roleName ?? string.Empty,
                    ["ruleId"] = conflict.RuleId,
                    ["ruleName"] = conflict.RuleName,
                    ["enforcement"] = conflict.Enforcement.ToString(),
                    ["conflictingPermissions"] = conflict.ConflictingPermissions,
                    ["triggeringPermissions"] = conflict.TriggeringPermissions
                },
                Severity = conflict.Enforcement == SoDEnforcementLevel.Strict ? "Error" : "Warning",
                Success = false
            };

            await _auditService.LogAsync(auditEvent, ct);
        }
    }
}
