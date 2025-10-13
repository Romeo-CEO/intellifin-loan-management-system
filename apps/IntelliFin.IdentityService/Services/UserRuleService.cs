using System.Linq;
using IntelliFin.IdentityService.Constants;
using IntelliFin.IdentityService.Models;
using IntelliFin.Shared.DomainModels.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Serilog;

namespace IntelliFin.IdentityService.Services;

/// <summary>
/// Service for managing user rules and JWT claims population
/// </summary>
public class UserRuleService : IUserRuleService
{
    private readonly LmsDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly IRoleService _roleService;
    private readonly ILogger<UserRuleService> _logger;
    
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(30);
    private const string USER_RULES_CACHE_KEY = "user_rules_{0}_{1}";
    private const string ROLE_RULES_CACHE_KEY = "role_rules_{0}_{1}";

    public UserRuleService(
        LmsDbContext context,
        IMemoryCache cache,
        IRoleService roleService,
        ILogger<UserRuleService> logger)
    {
        _context = context;
        _cache = cache;
        _roleService = roleService;
        _logger = logger;
    }

    public async Task<Dictionary<string, string>> GetUserRulesAsync(string userId, Guid? tenantId = null)
    {
        try
        {
            var cacheKey = string.Format(USER_RULES_CACHE_KEY, userId, tenantId?.ToString() ?? "null");
            
            if (_cache.TryGetValue(cacheKey, out Dictionary<string, string>? cachedRules))
            {
                return cachedRules ?? new Dictionary<string, string>();
            }

            // Get user's roles
            var userRoles = await GetUserRolesAsync(userId, tenantId);
            if (!userRoles.Any())
            {
                _logger.LogInformation("No roles found for user {UserId} in tenant {TenantId}", userId, tenantId);
                return new Dictionary<string, string>();
            }

            // Get rules for each role
            var roleRulesList = new List<Dictionary<string, string>>();
            foreach (var roleId in userRoles)
            {
                var roleRules = await GetRoleRulesAsync(roleId, tenantId);
                if (roleRules.Any())
                {
                    roleRulesList.Add(roleRules);
                }
            }

            // Resolve conflicts between roles
            var effectiveRules = await ResolveRuleConflictsAsync(roleRulesList);

            // Cache the result
            _cache.Set(cacheKey, effectiveRules, CacheExpiry);

            _logger.LogInformation("Retrieved {RuleCount} effective rules for user {UserId}", 
                effectiveRules.Count, userId);

            return effectiveRules;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting rules for user {UserId}", userId);
            return new Dictionary<string, string>();
        }
    }

    public async Task<Dictionary<string, Dictionary<string, string>>> GetUsersRulesAsync(string[] userIds, Guid? tenantId = null)
    {
        var result = new Dictionary<string, Dictionary<string, string>>();

        // Process users in parallel batches for performance
        var batchSize = 10;
        var batches = userIds.Chunk(batchSize);

        foreach (var batch in batches)
        {
            var batchTasks = batch.Select(async userId =>
            {
                var rules = await GetUserRulesAsync(userId, tenantId);
                return new { UserId = userId, Rules = rules };
            });

            var batchResults = await Task.WhenAll(batchTasks);
            
            foreach (var userResult in batchResults)
            {
                result[userResult.UserId] = userResult.Rules;
            }
        }

        return result;
    }

    public async Task<Dictionary<string, string>> GetRoleRulesAsync(string roleId, Guid? tenantId = null)
    {
        try
        {
            var cacheKey = string.Format(ROLE_RULES_CACHE_KEY, roleId, tenantId?.ToString() ?? "null");
            
            if (_cache.TryGetValue(cacheKey, out Dictionary<string, string>? cachedRules))
            {
                return cachedRules ?? new Dictionary<string, string>();
            }

            // Get role rules from the role service
            var roleRules = await _roleService.GetRoleRulesAsync(roleId, tenantId ?? Guid.Empty);
            
            var rulesDict = roleRules
                .Where(r => r.IsActive)
                .ToDictionary(r => r.RuleType, r => r.RuleValue);

            // Cache the result
            _cache.Set(cacheKey, rulesDict, CacheExpiry);

            return rulesDict;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting rules for role {RoleId}", roleId);
            return new Dictionary<string, string>();
        }
    }

    public async Task<Dictionary<string, string>> ResolveRuleConflictsAsync(
        List<Dictionary<string, string>> roleRules, 
        RuleConflictResolutionStrategy strategy = RuleConflictResolutionStrategy.TakeMaximum)
    {
        if (!roleRules.Any())
            return new Dictionary<string, string>();

        if (roleRules.Count == 1)
            return roleRules[0];

        var resolvedRules = new Dictionary<string, string>();
        var allRuleTypes = roleRules.SelectMany(r => r.Keys).Distinct();

        foreach (var ruleType in allRuleTypes)
        {
            var valuesForRule = roleRules
                .Where(r => r.ContainsKey(ruleType))
                .Select(r => r[ruleType])
                .ToList();

            if (valuesForRule.Count == 1)
            {
                resolvedRules[ruleType] = valuesForRule[0];
                continue;
            }

            // Apply conflict resolution strategy
            var resolvedValue = strategy switch
            {
                RuleConflictResolutionStrategy.TakeMaximum => ResolveMaximumValue(ruleType, valuesForRule),
                RuleConflictResolutionStrategy.TakeMinimum => ResolveMinimumValue(ruleType, valuesForRule),
                RuleConflictResolutionStrategy.TakeFirst => valuesForRule[0],
                RuleConflictResolutionStrategy.TakeLast => valuesForRule[^1],
                RuleConflictResolutionStrategy.RequireConsistency => ResolveConsistentValue(ruleType, valuesForRule),
                _ => ResolveMaximumValue(ruleType, valuesForRule)
            };

            if (!string.IsNullOrEmpty(resolvedValue))
            {
                resolvedRules[ruleType] = resolvedValue;
                
                if (valuesForRule.Count > 1)
                {
                    _logger.LogInformation("Resolved rule conflict for {RuleType}: {Values} -> {ResolvedValue} using {Strategy}", 
                        ruleType, string.Join(", ", valuesForRule), resolvedValue, strategy);
                }
            }
        }

        return resolvedRules;
    }

    public async Task<UserRuleComplianceResult> ValidateUserRuleComplianceAsync(string userId, Guid? tenantId = null)
    {
        try
        {
            var userRules = await GetUserRulesAsync(userId, tenantId);
            var hierarchy = await GetUserRuleHierarchyAsync(userId, tenantId);

            var result = new UserRuleComplianceResult
            {
                UserId = userId,
                TenantId = tenantId,
                IsCompliant = true,
                ComplianceScore = 100,
                Violations = new List<string>(),
                Warnings = new List<string>(),
                CheckedAt = DateTime.UtcNow
            };

            // Check for rule conflicts
            if (hierarchy.ConflictingRules.Any())
            {
                result.Warnings.AddRange(hierarchy.ConflictingRules.Select(c => 
                    $"Rule conflict detected: {c.RuleType} has conflicting values from roles {string.Join(", ", c.ConflictingRoles)}"));
            }

            // Check for missing required rules
            var requiredRules = GetRequiredRulesForTenant(tenantId);
            var missingRules = requiredRules.Where(r => !userRules.ContainsKey(r)).ToList();
            
            if (missingRules.Any())
            {
                result.IsCompliant = false;
                result.Violations.AddRange(missingRules.Select(r => $"Required rule missing: {r}"));
            }

            // Check for invalid rule values
            foreach (var rule in userRules)
            {
                var validation = ValidateRuleValue(rule.Key, rule.Value);
                if (!validation.IsValid)
                {
                    result.IsCompliant = false;
                    result.Violations.Add($"Invalid value for rule {rule.Key}: {validation.Error}");
                }
            }

            // Calculate compliance score
            var totalChecks = requiredRules.Length + userRules.Count;
            var violationCount = result.Violations.Count;
            
            if (totalChecks > 0)
            {
                result.ComplianceScore = Math.Max(0, 100 - (violationCount * 100 / totalChecks));
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating user rule compliance for {UserId}", userId);
            
            return new UserRuleComplianceResult
            {
                UserId = userId,
                TenantId = tenantId,
                IsCompliant = false,
                ComplianceScore = 0,
                Violations = new List<string> { $"Compliance check failed: {ex.Message}" },
                Warnings = new List<string>(),
                CheckedAt = DateTime.UtcNow
            };
        }
    }

    public async Task<UserRuleHierarchy> GetUserRuleHierarchyAsync(string userId, Guid? tenantId = null)
    {
        try
        {
            var userRoles = await GetUserRolesAsync(userId, tenantId);
            var hierarchy = new UserRuleHierarchy
            {
                UserId = userId,
                TenantId = tenantId,
                RoleRules = new Dictionary<string, Dictionary<string, string>>(),
                ConflictingRules = new List<RuleConflict>(),
                EffectiveRules = new Dictionary<string, string>(),
                GeneratedAt = DateTime.UtcNow
            };

            // Get rules for each role
            foreach (var roleId in userRoles)
            {
                var roleRules = await GetRoleRulesAsync(roleId, tenantId);
                hierarchy.RoleRules[roleId] = roleRules;
            }

            // Identify conflicts
            var allRuleTypes = hierarchy.RoleRules.Values.SelectMany(r => r.Keys).Distinct();
            
            foreach (var ruleType in allRuleTypes)
            {
                var rolesWithRule = hierarchy.RoleRules
                    .Where(kvp => kvp.Value.ContainsKey(ruleType))
                    .ToList();

                if (rolesWithRule.Count > 1)
                {
                    var values = rolesWithRule.Select(kvp => kvp.Value[ruleType]).Distinct().ToList();
                    
                    if (values.Count > 1)
                    {
                        hierarchy.ConflictingRules.Add(new RuleConflict
                        {
                            RuleType = ruleType,
                            ConflictingRoles = rolesWithRule.Select(kvp => kvp.Key).ToArray(),
                            ConflictingValues = values.ToArray()
                        });
                    }
                }
            }

            // Get effective rules (after conflict resolution)
            hierarchy.EffectiveRules = await GetUserRulesAsync(userId, tenantId);

            return hierarchy;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user rule hierarchy for {UserId}", userId);
            throw;
        }
    }

    public async Task RefreshUserRulesCacheAsync(string userId, Guid? tenantId = null)
    {
        try
        {
            var cacheKey = string.Format(USER_RULES_CACHE_KEY, userId, tenantId?.ToString() ?? "null");
            _cache.Remove(cacheKey);

            // Pre-populate cache
            await GetUserRulesAsync(userId, tenantId);

            _logger.LogInformation("Refreshed rules cache for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing user rules cache for {UserId}", userId);
            throw;
        }
    }

    public async Task<string[]> GetUsersAffectedByRuleChangeAsync(string ruleType, Guid? tenantId = null)
    {
        try
        {
            // Get all roles that have this rule
            var rolesWithRule = await GetRolesWithRuleAsync(ruleType, tenantId);
            
            // Get all users in those roles
            var affectedUsers = new HashSet<string>();
            
            foreach (var roleId in rolesWithRule)
            {
                var usersInRole = await GetUsersInRoleAsync(roleId, tenantId);
                foreach (var userId in usersInRole)
                {
                    affectedUsers.Add(userId);
                }
            }

            _logger.LogInformation("Rule change for {RuleType} affects {UserCount} users", 
                ruleType, affectedUsers.Count);

            return affectedUsers.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users affected by rule change {RuleType}", ruleType);
            return Array.Empty<string>();
        }
    }

    public async Task<UserClaims> PopulateUserClaimsWithRulesAsync(UserClaims userClaims)
    {
        try
        {
            var tenantId = !string.IsNullOrEmpty(userClaims.TenantId) 
                ? Guid.Parse(userClaims.TenantId) 
                : (Guid?)null;

            var userRules = await GetUserRulesAsync(userClaims.UserId, tenantId);
            
            // Create a new UserClaims object with rules populated
            var enrichedClaims = new UserClaims
            {
                UserId = userClaims.UserId,
                Username = userClaims.Username,
                Email = userClaims.Email,
                FirstName = userClaims.FirstName,
                LastName = userClaims.LastName,
                Roles = userClaims.Roles,
                Permissions = userClaims.Permissions,
                BranchId = userClaims.BranchId,
                BranchName = userClaims.BranchName,
                BranchRegion = userClaims.BranchRegion,
                TenantId = userClaims.TenantId,
                SessionId = userClaims.SessionId,
                DeviceId = userClaims.DeviceId,
                AuthenticatedAt = userClaims.AuthenticatedAt,
                AuthenticationLevel = userClaims.AuthenticationLevel,
                IpAddress = userClaims.IpAddress,
                Rules = userRules
            };

            _logger.LogInformation("Populated {RuleCount} rules for user {UserId} in JWT claims", 
                userRules.Count, userClaims.UserId);

            return enrichedClaims;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error populating user claims with rules for {UserId}", userClaims.UserId);
            
            // Return original claims if rule population fails
            return userClaims;
        }
    }

    #region Private Helper Methods

    private async Task<string[]> GetUserRolesAsync(string userId, Guid? tenantId)
    {
        // This would query user-role relationships from the database
        // For now, return mock data
        return new[] { "role-123", "role-456" };
    }

    private string ResolveMaximumValue(string ruleType, List<string> values)
    {
        if (!values.Any()) return string.Empty;

        try
        {
            // For numeric rules, take the maximum value
            if (IsNumericRule(ruleType))
            {
                var numericValues = values
                    .Where(v => decimal.TryParse(v, out _))
                    .Select(decimal.Parse)
                    .ToList();
                
                return numericValues.Any() ? numericValues.Max().ToString() : values[0];
            }

            // For grade rules, take the highest risk grade
            if (IsGradeRule(ruleType))
            {
                var gradeOrder = new Dictionary<string, int> { ["A"] = 1, ["B"] = 2, ["C"] = 3, ["D"] = 4, ["F"] = 5 };
                var validGrades = values.Where(v => gradeOrder.ContainsKey(v.ToUpperInvariant())).ToList();
                
                return validGrades.Any() 
                    ? validGrades.OrderByDescending(g => gradeOrder[g.ToUpperInvariant()]).First()
                    : values[0];
            }

            // For other rules, take the first value
            return values[0];
        }
        catch
        {
            return values[0];
        }
    }

    private string ResolveMinimumValue(string ruleType, List<string> values)
    {
        if (!values.Any()) return string.Empty;

        try
        {
            // For numeric rules, take the minimum value
            if (IsNumericRule(ruleType))
            {
                var numericValues = values
                    .Where(v => decimal.TryParse(v, out _))
                    .Select(decimal.Parse)
                    .ToList();
                
                return numericValues.Any() ? numericValues.Min().ToString() : values[0];
            }

            // For grade rules, take the lowest risk grade
            if (IsGradeRule(ruleType))
            {
                var gradeOrder = new Dictionary<string, int> { ["A"] = 1, ["B"] = 2, ["C"] = 3, ["D"] = 4, ["F"] = 5 };
                var validGrades = values.Where(v => gradeOrder.ContainsKey(v.ToUpperInvariant())).ToList();
                
                return validGrades.Any() 
                    ? validGrades.OrderBy(g => gradeOrder[g.ToUpperInvariant()]).First()
                    : values[0];
            }

            // For other rules, take the first value
            return values[0];
        }
        catch
        {
            return values[0];
        }
    }

    private string ResolveConsistentValue(string ruleType, List<string> values)
    {
        var distinctValues = values.Distinct().ToList();
        
        if (distinctValues.Count == 1)
        {
            return distinctValues[0];
        }

        // If values are inconsistent, log warning and return empty
        _logger.LogWarning("Inconsistent values for rule {RuleType}: {Values}", ruleType, string.Join(", ", values));
        return string.Empty;
    }

    private bool IsNumericRule(string ruleType)
    {
        return ruleType.Contains("limit") || ruleType.Contains("amount") || 
               ruleType.Contains("count") || ruleType.Contains("threshold") ||
               ruleType.Contains("term") || ruleType.Contains("period");
    }

    private bool IsGradeRule(string ruleType)
    {
        return ruleType.Contains("grade") || ruleType.Contains("risk");
    }

    private string[] GetRequiredRulesForTenant(Guid? tenantId)
    {
        // This would determine required rules based on tenant type and subscription
        return new[]
        {
            SystemRules.LoanApprovalLimit,
            SystemRules.MaxRiskGrade,
            SystemRules.AuditTrailLevel
        };
    }

    private (bool IsValid, string Error) ValidateRuleValue(string ruleType, string value)
    {
        try
        {
            // Basic validation - would be more comprehensive in real implementation
            if (string.IsNullOrWhiteSpace(value))
                return (false, "Value cannot be empty");

            if (IsNumericRule(ruleType))
            {
                if (!decimal.TryParse(value, out var numValue))
                    return (false, "Value must be numeric");
                
                if (numValue < 0)
                    return (false, "Value cannot be negative");
            }

            if (IsGradeRule(ruleType))
            {
                var validGrades = new[] { "A", "B", "C", "D", "F" };
                if (!validGrades.Contains(value.ToUpperInvariant()))
                    return (false, "Grade must be A, B, C, D, or F");
            }

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, $"Validation error: {ex.Message}");
        }
    }

    private async Task<string[]> GetRolesWithRuleAsync(string ruleType, Guid? tenantId)
    {
        // This would query the database for roles that have this rule
        return new[] { "role-123", "role-456" };
    }

    private async Task<string[]> GetUsersInRoleAsync(string roleId, Guid? tenantId)
    {
        // This would query the database for users in this role
        return new[] { "user-123", "user-456" };
    }

    #endregion
}