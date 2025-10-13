using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IntelliFin.AdminService.Data;
using IntelliFin.AdminService.Models;
using IntelliFin.AdminService.Models.Keycloak;
using IntelliFin.Shared.DomainModels.Data;
using IntelliFin.Shared.DomainModels.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IntelliFin.AdminService.Services;

public sealed class KeycloakManagerDirectoryService : IManagerDirectoryService
{
    private static readonly string[] RiskLevelLabels = ["Low", "Medium", "High", "Critical"];

    private readonly IKeycloakAdminService _keycloakAdminService;
    private readonly IDbContextFactory<LmsDbContext> _lmsDbContextFactory;
    private readonly AdminDbContext _adminDbContext;
    private readonly ILogger<KeycloakManagerDirectoryService> _logger;

    public KeycloakManagerDirectoryService(
        IKeycloakAdminService keycloakAdminService,
        IDbContextFactory<LmsDbContext> lmsDbContextFactory,
        AdminDbContext adminDbContext,
        ILogger<KeycloakManagerDirectoryService> logger)
    {
        _keycloakAdminService = keycloakAdminService;
        _lmsDbContextFactory = lmsDbContextFactory;
        _adminDbContext = adminDbContext;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ManagerUserAssignment>> GetManagerUserAssignmentsAsync(CancellationToken cancellationToken)
    {
        var users = await LoadAllKeycloakUsersAsync(cancellationToken);
        if (users.Count == 0)
        {
            _logger.LogWarning("Keycloak returned zero users for recertification campaign generation");
            return Array.Empty<ManagerUserAssignment>();
        }

        var userIndex = users.ToDictionary(u => u.Id, StringComparer.OrdinalIgnoreCase);
        var roleDefinitions = await _adminDbContext.RoleDefinitions.AsNoTracking().ToListAsync(cancellationToken);
        var sodPolicies = await _adminDbContext.SodPolicies.AsNoTracking()
            .Where(p => p.Enabled)
            .ToListAsync(cancellationToken);
        var keycloakToAspNet = await _adminDbContext.UserIdMappings.AsNoTracking()
            .ToDictionaryAsync(m => m.KeycloakUserId, m => m.AspNetUserId, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var aspNetIds = keycloakToAspNet.Values.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var identityUsers = new Dictionary<string, User>(StringComparer.OrdinalIgnoreCase);
        if (aspNetIds.Count > 0)
        {
            await using var identityDb = await _lmsDbContextFactory.CreateDbContextAsync(cancellationToken);
            var tracked = await identityDb.Users
                .AsNoTracking()
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                        .ThenInclude(r => r.RolePermissions)
                            .ThenInclude(rp => rp.Permission)
                .Where(u => aspNetIds.Contains(u.Id))
                .ToListAsync(cancellationToken);
            foreach (var user in tracked)
            {
                identityUsers[user.Id] = user;
            }
        }

        var assignments = new List<ManagerUserAssignment>();
        foreach (var user in users)
        {
            if (!TryGetAttribute(user.Attributes, "managerId", out var managerId) || string.IsNullOrWhiteSpace(managerId))
            {
                continue;
            }

            if (!userIndex.TryGetValue(managerId, out var manager))
            {
                _logger.LogWarning("Unable to resolve manager {ManagerId} for user {UserId}", managerId, user.Id);
                continue;
            }

            var roles = await _keycloakAdminService.GetUserRolesAsync(user.Id, cancellationToken);
            var normalizedRoles = roles
                .Select(r => r.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var permissionSet = ResolvePermissions(user.Id, keycloakToAspNet, identityUsers);
            var riskIndicators = new List<string>();
            var riskLevel = DetermineRiskLevel(normalizedRoles, roleDefinitions, riskIndicators);
            EvaluateSodConflicts(normalizedRoles, sodPolicies, riskIndicators, ref riskLevel);
            EvaluateDormantAccount(user, keycloakToAspNet, identityUsers, riskIndicators, ref riskLevel);

            var (department, jobTitle) = ResolveOrgDetails(user, keycloakToAspNet, identityUsers);
            var lastLogin = ResolveLastLogin(user, keycloakToAspNet, identityUsers);
            var accessGranted = ResolveAccessGranted(user, keycloakToAspNet, identityUsers);

            var assignment = new ManagerUserAssignment(
                user.Id,
                ComposeDisplayName(user.FirstName, user.LastName, user.Username),
                user.Email ?? string.Empty,
                department,
                jobTitle,
                manager.Id,
                ComposeDisplayName(manager.FirstName, manager.LastName, manager.Username),
                manager.Email ?? string.Empty,
                normalizedRoles,
                permissionSet,
                riskLevel,
                riskIndicators,
                lastLogin,
                accessGranted);

            assignments.Add(assignment);
        }

        return assignments;
    }

    private static string ComposeDisplayName(string? firstName, string? lastName, string fallback)
    {
        var name = string.Join(' ', new[] { firstName, lastName }
            .Where(part => !string.IsNullOrWhiteSpace(part)));
        return string.IsNullOrWhiteSpace(name) ? fallback : name;
    }

    private async Task<List<UserResponse>> LoadAllKeycloakUsersAsync(CancellationToken cancellationToken)
    {
        var results = new List<UserResponse>();
        const int pageSize = 100;
        var page = 1;

        while (true)
        {
            var paged = await _keycloakAdminService.GetUsersAsync(page, pageSize, cancellationToken);
            if (paged.Items.Count == 0)
            {
                break;
            }

            results.AddRange(paged.Items);

            if (results.Count >= paged.TotalCount)
            {
                break;
            }

            page++;
        }

        return results;
    }

    private static bool TryGetAttribute(
        IReadOnlyDictionary<string, IReadOnlyList<string>>? attributes,
        string key,
        out string? value)
    {
        value = null;
        if (attributes is null)
        {
            return false;
        }

        foreach (var pair in attributes)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase) && pair.Value.Count > 0)
            {
                value = pair.Value[0];
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<string> ResolvePermissions(
        string keycloakUserId,
        IReadOnlyDictionary<string, string> keycloakToAspNet,
        IReadOnlyDictionary<string, User> identityUsers)
    {
        if (!keycloakToAspNet.TryGetValue(keycloakUserId, out var aspNetUserId))
        {
            return Array.Empty<string>();
        }

        if (!identityUsers.TryGetValue(aspNetUserId, out var identityUser))
        {
            return Array.Empty<string>();
        }

        var permissions = identityUser.UserRoles
            .Where(ur => ur.IsActive && ur.Role?.RolePermissions is not null)
            .SelectMany(ur => ur.Role.RolePermissions)
            .Where(rp => rp.IsActive && rp.Permission is not null && rp.Permission.IsActive)
            .Select(rp => rp.Permission.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return permissions;
    }

    private static string DetermineRiskLevel(
        IReadOnlyCollection<string> roles,
        IReadOnlyCollection<RoleDefinition> roleDefinitions,
        IList<string> indicators)
    {
        var level = RiskLevel.Low;
        var definitionLookup = roleDefinitions
            .Where(r => !string.IsNullOrWhiteSpace(r.RoleName))
            .ToDictionary(r => r.RoleName, r => r, StringComparer.OrdinalIgnoreCase);

        foreach (var role in roles)
        {
            if (!definitionLookup.TryGetValue(role, out var definition))
            {
                continue;
            }

            switch (definition.RiskLevel?.Trim().ToUpperInvariant())
            {
                case "CRITICAL":
                    indicators.Add($"Critical privilege role: {definition.DisplayName ?? definition.RoleName}");
                    level = RiskLevel.Critical;
                    break;
                case "HIGH":
                    indicators.Add($"High privilege role: {definition.DisplayName ?? definition.RoleName}");
                    level = Max(level, RiskLevel.High);
                    break;
                case "MEDIUM":
                    level = Max(level, RiskLevel.Medium);
                    break;
                case "LOW":
                default:
                    level = Max(level, RiskLevel.Low);
                    break;
            }

            if (level == RiskLevel.Critical)
            {
                break;
            }
        }

        return RiskLevelLabels[(int)level];
    }

    private static void EvaluateSodConflicts(
        IReadOnlyCollection<string> roles,
        IReadOnlyCollection<SodPolicy> policies,
        IList<string> indicators,
        ref string riskLevel)
    {
        var roleSet = roles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var currentLevel = ParseRiskLevel(riskLevel);

        foreach (var policy in policies)
        {
            if (roleSet.Contains(policy.Role1) && roleSet.Contains(policy.Role2))
            {
                indicators.Add($"SoD conflict detected: {policy.Role1} + {policy.Role2} ({policy.Severity})");
                var severity = policy.Severity?.Trim().ToUpperInvariant();
                currentLevel = severity switch
                {
                    "CRITICAL" => RiskLevel.Critical,
                    "HIGH" => Max(currentLevel, RiskLevel.High),
                    "MEDIUM" => Max(currentLevel, RiskLevel.Medium),
                    _ => Max(currentLevel, RiskLevel.Medium)
                };
            }
        }

        riskLevel = RiskLevelLabels[(int)currentLevel];
    }

    private static void EvaluateDormantAccount(
        UserResponse user,
        IReadOnlyDictionary<string, string> keycloakToAspNet,
        IReadOnlyDictionary<string, User> identityUsers,
        IList<string> indicators,
        ref string riskLevel)
    {
        var lastLogin = ResolveLastLogin(user, keycloakToAspNet, identityUsers);
        if (lastLogin is null)
        {
            return;
        }

        if (DateTime.UtcNow - lastLogin > TimeSpan.FromDays(90))
        {
            indicators.Add("Dormant account (>90 days without login)");
            var level = ParseRiskLevel(riskLevel);
            level = Max(level, RiskLevel.Medium);
            riskLevel = RiskLevelLabels[(int)level];
        }
    }

    private static (string? Department, string? JobTitle) ResolveOrgDetails(
        UserResponse user,
        IReadOnlyDictionary<string, string> keycloakToAspNet,
        IReadOnlyDictionary<string, User> identityUsers)
    {
        if (TryGetAttribute(user.Attributes, "department", out var department) && !string.IsNullOrWhiteSpace(department))
        {
            department = department.Trim();
        }
        else if (keycloakToAspNet.TryGetValue(user.Id, out var aspNetId) && identityUsers.TryGetValue(aspNetId, out var identity))
        {
            department = identity.Metadata.TryGetValue("department", out var value) ? value?.ToString() : null;
        }

        if (TryGetAttribute(user.Attributes, "jobTitle", out var jobTitle) && !string.IsNullOrWhiteSpace(jobTitle))
        {
            jobTitle = jobTitle.Trim();
        }
        else if (keycloakToAspNet.TryGetValue(user.Id, out var aspId) && identityUsers.TryGetValue(aspId, out var identityUser))
        {
            jobTitle = identityUser.Metadata.TryGetValue("jobTitle", out var value) ? value?.ToString() : null;
        }

        return (department, jobTitle);
    }

    private static DateTime? ResolveLastLogin(
        UserResponse user,
        IReadOnlyDictionary<string, string> keycloakToAspNet,
        IReadOnlyDictionary<string, User> identityUsers)
    {
        if (TryGetAttribute(user.Attributes, "lastLoginAt", out var lastLogin) && DateTime.TryParse(lastLogin, out var parsed))
        {
            return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        }

        if (keycloakToAspNet.TryGetValue(user.Id, out var aspNetId) && identityUsers.TryGetValue(aspNetId, out var identityUser))
        {
            return identityUser.LastLoginAt;
        }

        return null;
    }

    private static DateTime? ResolveAccessGranted(
        UserResponse user,
        IReadOnlyDictionary<string, string> keycloakToAspNet,
        IReadOnlyDictionary<string, User> identityUsers)
    {
        if (TryGetAttribute(user.Attributes, "accessGrantedAt", out var granted) && DateTime.TryParse(granted, out var parsed))
        {
            return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        }

        if (keycloakToAspNet.TryGetValue(user.Id, out var aspNetId) && identityUsers.TryGetValue(aspNetId, out var identityUser))
        {
            return identityUser.CreatedAt;
        }

        return null;
    }

    private static RiskLevel ParseRiskLevel(string value)
    {
        return value?.Trim().ToUpperInvariant() switch
        {
            "CRITICAL" => RiskLevel.Critical,
            "HIGH" => RiskLevel.High,
            "MEDIUM" => RiskLevel.Medium,
            _ => RiskLevel.Low
        };
    }

    private static RiskLevel Max(RiskLevel left, RiskLevel right) => (RiskLevel)Math.Max((int)left, (int)right);

    private enum RiskLevel
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Critical = 3
    }
}
