using IntelliFin.IdentityService.Models;

namespace IntelliFin.IdentityService.Services;

/// <summary>
/// Service for runtime authorization checks and permission evaluation
/// </summary>
public interface IAuthorizationService
{
    /// <summary>
    /// Checks if a user has a specific permission
    /// </summary>
    Task<bool> HasPermissionAsync(string userId, string permission, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the current user has a specific permission
    /// </summary>
    Task<bool> HasPermissionAsync(string permission, CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates a rule for the current user context
    /// </summary>
    Task<RuleEvaluationResult> EvaluateRuleAsync(string ruleType, object value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates a rule for a specific user context
    /// </summary>
    Task<RuleEvaluationResult> EvaluateRuleAsync(string userId, string ruleType, object value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all permissions for a user
    /// </summary>
    Task<string[]> GetUserPermissionsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all permissions for the current user
    /// </summary>
    Task<string[]> GetMyPermissionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets effective rules for a user (combining role rules with user-specific rules)
    /// </summary>
    Task<Dictionary<string, string>> GetUserRulesAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets effective rules for the current user
    /// </summary>
    Task<Dictionary<string, string>> GetMyRulesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates if a user can perform an action based on permissions and rules
    /// </summary>
    Task<AuthorizationResult> ValidateActionAsync(string action, object? context = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates if a specific user can perform an action
    /// </summary>
    Task<AuthorizationResult> ValidateActionAsync(string userId, string action, object? context = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk permission check for multiple permissions
    /// </summary>
    Task<Dictionary<string, bool>> HasPermissionsAsync(string[] permissions, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk permission check for a specific user
    /// </summary>
    Task<Dictionary<string, bool>> HasPermissionsAsync(string userId, string[] permissions, CancellationToken cancellationToken = default);
}
