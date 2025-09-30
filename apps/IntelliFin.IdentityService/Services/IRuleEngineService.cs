using IntelliFin.IdentityService.Models;
using System.Security.Claims;

namespace IntelliFin.IdentityService.Services;

/// <summary>
/// Service for runtime evaluation of business rules
/// </summary>
public interface IRuleEngineService
{
    /// <summary>
    /// Evaluates a specific rule against a context value for a user
    /// </summary>
    Task<RuleEvaluationResult> EvaluateRuleAsync(string ruleType, string ruleValue, object contextValue, CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates multiple rules in a batch for performance
    /// </summary>
    Task<Dictionary<string, RuleEvaluationResult>> EvaluateRulesAsync(Dictionary<string, object> ruleContexts, ClaimsPrincipal user);

    /// <summary>
    /// Gets all rule values for a user from their claims
    /// </summary>
    Dictionary<string, string> GetUserRules(ClaimsPrincipal user);

    /// <summary>
    /// Validates rule configuration against business constraints
    /// </summary>
    Task<RuleValidationResult> ValidateRuleConfigurationAsync(RoleRule ruleConfiguration, Guid? tenantId = null);

    /// <summary>
    /// Tests rule evaluation with sample scenarios
    /// </summary>
    Task<RuleTestResult> TestRuleAsync(RuleTestScenario scenario);

    /// <summary>
    /// Checks if user has sufficient authority for a specific business operation
    /// </summary>
    Task<AuthorityCheckResult> CheckBusinessAuthorityAsync(string operation, object context, ClaimsPrincipal user);

    /// <summary>
    /// Gets effective rule value considering role hierarchy and inheritance
    /// </summary>
    Task<string?> GetEffectiveRuleValueAsync(string ruleType, ClaimsPrincipal user);

    /// <summary>
    /// Validates compliance requirements for rule configuration
    /// </summary>
    Task<ComplianceValidationResult> ValidateComplianceAsync(List<RoleRule> rules, Guid tenantId);
}