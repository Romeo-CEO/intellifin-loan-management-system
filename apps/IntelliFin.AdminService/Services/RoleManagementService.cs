using System.Text.Json;
using IntelliFin.AdminService.Contracts.Requests;
using IntelliFin.AdminService.Contracts.Responses;
using IntelliFin.AdminService.Data;
using IntelliFin.AdminService.ExceptionHandling;
using IntelliFin.AdminService.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IntelliFin.AdminService.Services;

public sealed class RoleManagementService : IRoleManagementService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly AdminDbContext _dbContext;
    private readonly IKeycloakAdminService _keycloakAdminService;
    private readonly IAuditService _auditService;
    private readonly ILogger<RoleManagementService> _logger;

    public RoleManagementService(
        AdminDbContext dbContext,
        IKeycloakAdminService keycloakAdminService,
        IAuditService auditService,
        ILogger<RoleManagementService> logger)
    {
        _dbContext = dbContext;
        _keycloakAdminService = keycloakAdminService;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<RoleDefinitionDto>> GetAllRolesAsync(CancellationToken cancellationToken)
    {
        var roles = await _dbContext.RoleDefinitions
            .AsNoTracking()
            .OrderBy(r => r.Category)
            .ThenBy(r => r.DisplayName)
            .Select(r => new RoleDefinitionDto(
                r.RoleName,
                r.DisplayName,
                r.Description,
                r.Category,
                r.RiskLevel,
                r.RequiresApproval))
            .ToListAsync(cancellationToken);

        return roles;
    }

    public async Task<UserRolesDto?> GetUserRolesAsync(string userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var assignedRoles = await _keycloakAdminService.GetUserRolesAsync(userId, cancellationToken);
        var roleNames = assignedRoles.Select(r => r.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();

        var definitions = await _dbContext.RoleDefinitions
            .AsNoTracking()
            .Where(r => roleNames.Contains(r.RoleName))
            .ToDictionaryAsync(r => r.RoleName, cancellationToken);

        var mappedRoles = roleNames
            .Select(roleName =>
            {
                var definition = definitions.GetValueOrDefault(roleName);
                return new UserRoleDto(
                    roleName,
                    definition?.DisplayName ?? roleName,
                    definition?.Category,
                    definition?.RiskLevel,
                    null);
            })
            .ToList();

        var activeExceptions = await _dbContext.SodExceptions
            .AsNoTracking()
            .Where(e => e.UserId == userId && e.Status == "Active" && e.ExpiresAt > DateTime.UtcNow)
            .Select(e => new SodExceptionSummaryDto(
                e.ExceptionId,
                e.RequestedRole,
                e.ExpiresAt ?? DateTime.UtcNow,
                e.BusinessJustification,
                e.ReviewedBy))
            .ToListAsync(cancellationToken);

        return new UserRolesDto(userId, mappedRoles, activeExceptions);
    }

    public async Task<RoleAssignmentResult> AssignRoleAsync(
        string userId,
        string roleName,
        bool confirmedSodOverride,
        string adminId,
        string adminName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(roleName);
        ArgumentException.ThrowIfNullOrWhiteSpace(adminId);

        var currentRoles = await _keycloakAdminService.GetUserRolesAsync(userId, cancellationToken);
        var existingRoleNames = currentRoles
            .Select(r => r.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();

        var conflicts = await GetSodConflictsAsync(existingRoleNames, roleName, cancellationToken);
        if (conflicts.Count > 0)
        {
            var criticalConflicts = conflicts
                .Where(c => string.Equals(c.Severity, "Critical", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (criticalConflicts.Count > 0)
            {
                await LogConflictAsync(adminId, userId, roleName, criticalConflicts, cancellationToken);
                throw new SodConflictException(
                    $"Critical SoD conflict detected for role '{roleName}'.",
                    criticalConflicts.Select(c => c.ConflictingRole).ToList(),
                    "Critical");
            }

            var highConflicts = conflicts
                .Where(c => string.Equals(c.Severity, "High", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (highConflicts.Count > 0 && !confirmedSodOverride)
            {
                throw new SodConflictException(
                    $"High severity SoD conflict detected for role '{roleName}'. Confirmation required.",
                    conflicts.Select(c => c.ConflictingRole).ToList(),
                    "High");
            }
        }

        await _keycloakAdminService.AssignRolesAsync(
            userId,
            new AssignRolesRequest { Roles = new[] { roleName } },
            cancellationToken);

        var assignmentId = Guid.NewGuid();

        await _auditService.LogEventAsync(new AuditEvent
        {
            Timestamp = DateTime.UtcNow,
            Actor = adminId,
            Action = "RoleAssigned",
            EntityType = "UserRole",
            EntityId = $"{userId}:{roleName}",
            EventData = JsonSerializer.Serialize(new
            {
                userId,
                roleName,
                assignedBy = adminName,
                confirmedSodOverride
            }, SerializerOptions)
        }, cancellationToken);

        _logger.LogInformation(
            "Assigned role {Role} to user {User} by admin {Admin}",
            roleName,
            userId,
            adminId);

        return new RoleAssignmentResult(assignmentId, true);
    }

    public async Task RemoveRoleAsync(
        string userId,
        string roleName,
        string adminId,
        string adminName,
        string? reason,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(roleName);
        ArgumentException.ThrowIfNullOrWhiteSpace(adminId);

        await _keycloakAdminService.RemoveRoleAsync(userId, roleName, cancellationToken);

        await _auditService.LogEventAsync(new AuditEvent
        {
            Timestamp = DateTime.UtcNow,
            Actor = adminId,
            Action = "RoleRemoved",
            EntityType = "UserRole",
            EntityId = $"{userId}:{roleName}",
            EventData = JsonSerializer.Serialize(new
            {
                userId,
                roleName,
                removedBy = adminName,
                reason
            }, SerializerOptions)
        }, cancellationToken);

        _logger.LogInformation(
            "Removed role {Role} from user {User} by admin {Admin}",
            roleName,
            userId,
            adminId);
    }

    public async Task<SodValidationResponse> ValidateSodAsync(
        string userId,
        string proposedRole,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(proposedRole);

        var currentRoles = await _keycloakAdminService.GetUserRolesAsync(userId, cancellationToken);
        var existing = currentRoles.Select(r => r.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
        var conflicts = await GetSodConflictsAsync(existing, proposedRole, cancellationToken);
        var canOverride = conflicts.All(c => !string.Equals(c.Severity, "Critical", StringComparison.OrdinalIgnoreCase));

        return new SodValidationResponse(conflicts.Count > 0, conflicts, canOverride);
    }

    public async Task<IReadOnlyCollection<RoleHierarchyDto>> GetRoleHierarchyAsync(CancellationToken cancellationToken)
    {
        var hierarchy = await _dbContext.RoleHierarchy
            .AsNoTracking()
            .OrderBy(r => r.ParentRole)
            .ThenBy(r => r.ChildRole)
            .Select(r => new RoleHierarchyDto(r.ParentRole, r.ChildRole))
            .ToListAsync(cancellationToken);

        return hierarchy;
    }

    public async Task<IReadOnlyCollection<SodPolicyDto>> GetPoliciesAsync(CancellationToken cancellationToken)
    {
        var policies = await _dbContext.SodPolicies
            .AsNoTracking()
            .OrderBy(p => p.Severity)
            .ThenBy(p => p.Role1)
            .Select(p => new SodPolicyDto(p.Id, p.Role1, p.Role2, p.ConflictDescription, p.Severity, p.Enabled))
            .ToListAsync(cancellationToken);

        return policies;
    }

    private async Task<List<SodConflictDto>> GetSodConflictsAsync(
        IReadOnlyCollection<string> existingRoles,
        string proposedRole,
        CancellationToken cancellationToken)
    {
        var normalizedExisting = existingRoles
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (normalizedExisting.Count == 0)
        {
            return new List<SodConflictDto>();
        }

        var policies = await _dbContext.SodPolicies
            .AsNoTracking()
            .Where(p => p.Enabled)
            .ToListAsync(cancellationToken);

        var conflicts = new List<SodConflictDto>();
        foreach (var policy in policies)
        {
            if (!string.Equals(policy.Role1, proposedRole, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(policy.Role2, proposedRole, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var conflictingRole = string.Equals(policy.Role1, proposedRole, StringComparison.OrdinalIgnoreCase)
                ? policy.Role2
                : policy.Role1;

            if (normalizedExisting.Contains(conflictingRole))
            {
                conflicts.Add(new SodConflictDto(
                    policy.Id,
                    conflictingRole,
                    policy.ConflictDescription,
                    policy.Severity));
            }
        }

        return conflicts;
    }

    private async Task LogConflictAsync(
        string adminId,
        string userId,
        string roleName,
        IReadOnlyCollection<SodConflictDto> conflicts,
        CancellationToken cancellationToken)
    {
        await _auditService.LogEventAsync(new AuditEvent
        {
            Timestamp = DateTime.UtcNow,
            Actor = adminId,
            Action = "SodConflictDetected",
            EntityType = "UserRole",
            EntityId = $"{userId}:{roleName}",
            EventData = JsonSerializer.Serialize(new
            {
                userId,
                roleName,
                conflicts
            }, SerializerOptions)
        }, cancellationToken);

        _logger.LogWarning(
            "SoD conflict detected when assigning role {Role} to user {User}. Conflicts: {Conflicts}",
            roleName,
            userId,
            string.Join(", ", conflicts.Select(c => c.ConflictingRole)));
    }
}

