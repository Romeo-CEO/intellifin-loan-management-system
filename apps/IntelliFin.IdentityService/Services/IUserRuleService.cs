using IntelliFin.IdentityService.Models;

namespace IntelliFin.IdentityService.Services;

/// <summary>
/// Service for managing user rules and populating JWT claims
/// </summary>
public interface IUserRuleService
{
    /// <summary>
    /// Gets all effective rules for a user based on their roles
    /// </summary>
    Task<Dictionary<string, string>> GetUserRulesAsync(string userId, Guid? tenantId = null);

    /// <summary>
    /// Gets effective rules for multiple users (bulk operation)
    /// </summary>
    Task<Dictionary<string, Dictionary<string, string>>> GetUsersRulesAsync(string[] userIds, Guid? tenantId = null);

    /// <summary>
    /// Gets effective rules for a specific role
    /// </summary>
    Task<Dictionary<string, string>> GetRoleRulesAsync(string roleId, Guid? tenantId = null);

    /// <summary>
    /// Resolves rule conflicts when user has multiple roles with different rule values
    /// </summary>
    Task<Dictionary<string, string>> ResolveRuleConflictsAsync(List<Dictionary<string, string>> roleRules, RuleConflictResolutionStrategy strategy = RuleConflictResolutionStrategy.TakeMaximum);

    /// <summary>
    /// Validates that user's effective rules meet compliance requirements
    /// </summary>
    Task<UserRuleComplianceResult> ValidateUserRuleComplianceAsync(string userId, Guid? tenantId = null);

    /// <summary>
    /// Gets rule inheritance hierarchy for a user
    /// </summary>
    Task<UserRuleHierarchy> GetUserRuleHierarchyAsync(string userId, Guid? tenantId = null);

    /// <summary>
    /// Refreshes user rules cache for a specific user
    /// </summary>
    Task RefreshUserRulesCacheAsync(string userId, Guid? tenantId = null);

    /// <summary>
    /// Gets users affected by a rule template change
    /// </summary>
    Task<string[]> GetUsersAffectedByRuleChangeAsync(string ruleType, Guid? tenantId = null);

    /// <summary>
    /// Populates UserClaims with rule data for JWT token generation
    /// </summary>
    Task<UserClaims> PopulateUserClaimsWithRulesAsync(UserClaims userClaims);
}