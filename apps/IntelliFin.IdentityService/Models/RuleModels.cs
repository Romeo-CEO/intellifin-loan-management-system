using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace IntelliFin.IdentityService.Models;

/// <summary>
/// Rule configuration for a specific role
/// Defines business rules with values that govern user behavior
/// </summary>
public class RoleRule
{
    /// <summary>
    /// Unique identifier for this rule configuration
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Role this rule applies to
    /// </summary>
    public string RoleId { get; set; } = string.Empty;

    /// <summary>
    /// Type of rule (from SystemRules constants)
    /// </summary>
    public string RuleType { get; set; } = string.Empty;

    /// <summary>
    /// Rule value (amount, count, grade, etc.)
    /// </summary>
    public string RuleValue { get; set; } = string.Empty;

    /// <summary>
    /// Type of value stored in RuleValue
    /// </summary>
    public RuleValueType ValueType { get; set; } = RuleValueType.Amount;

    /// <summary>
    /// Additional conditions that must be met
    /// </summary>
    public RuleCondition[] Conditions { get; set; } = Array.Empty<RuleCondition>();

    /// <summary>
    /// Whether this rule is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// When this rule was created
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Who created this rule
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// When this rule was last updated
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>
    /// Who last updated this rule
    /// </summary>
    public string? UpdatedBy { get; set; }

    /// <summary>
    /// Optional notes about this rule configuration
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Template this rule was created from (if any)
    /// </summary>
    public string? TemplateId { get; set; }

    /// <summary>
    /// Who assigned this rule to the role
    /// </summary>
    public string? AssignedBy { get; set; }

    /// <summary>
    /// When this rule was assigned to the role
    /// </summary>
    public DateTime? AssignedAt { get; set; }

    /// <summary>
    /// Who removed this rule (soft delete)
    /// </summary>
    public string? RemovedBy { get; set; }

    /// <summary>
    /// When this rule was removed (soft delete)
    /// </summary>
    public DateTime? RemovedAt { get; set; }

    /// <summary>
    /// Navigation property to the role
    /// </summary>
    public virtual ApplicationRole Role { get; set; } = null!;
}

/// <summary>
/// Types of values that can be stored in rules
/// </summary>
public enum RuleValueType
{
    /// <summary>
    /// Financial amounts (e.g., 50000.00)
    /// </summary>
    Amount = 1,

    /// <summary>
    /// Numeric counts (e.g., 5)
    /// </summary>
    Count = 2,

    /// <summary>
    /// Risk grades (e.g., A, B, C, D, F)
    /// </summary>
    Grade = 3,

    /// <summary>
    /// Time periods (e.g., 24h, 30d)
    /// </summary>
    Duration = 4,

    /// <summary>
    /// Access scopes (e.g., branch-001,branch-002)
    /// </summary>
    Scope = 5,

    /// <summary>
    /// Boolean flags (e.g., true, false)
    /// </summary>
    Boolean = 6,

    /// <summary>
    /// Enumeration values (e.g., basic,enhanced,premium)
    /// </summary>
    Enum = 7,

    /// <summary>
    /// Percentage values (e.g., 15.5)
    /// </summary>
    Percentage = 8,

    /// <summary>
    /// Time ranges (e.g., 09:00-17:00)
    /// </summary>
    TimeRange = 9,

    /// <summary>
    /// IP address ranges (e.g., 192.168.1.0/24)
    /// </summary>
    IpRange = 10
}

/// <summary>
/// Condition that must be met for a rule to apply
/// </summary>
public class RuleCondition
{
    /// <summary>
    /// Type of condition (e.g., "risk_grade", "loan_type")
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Operator for the condition (equals, in, greater_than, etc.)
    /// </summary>
    public string Operator { get; set; } = string.Empty;

    /// <summary>
    /// Single value for the condition
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// Multiple values for the condition (for 'in' operator)
    /// </summary>
    public string[] Values { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Human-readable description of this condition
    /// </summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Template for rule configuration
/// Defines the structure and constraints for a type of rule
/// </summary>
public class RuleTemplate
{
    /// <summary>
    /// Unique template identifier
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Template name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Detailed description of this rule type
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Category for organizing templates
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Type of value this rule accepts
    /// </summary>
    public RuleValueType ValueType { get; set; } = RuleValueType.Amount;

    /// <summary>
    /// Minimum allowed value (if applicable)
    /// </summary>
    public object? MinValue { get; set; }

    /// <summary>
    /// Maximum allowed value (if applicable)
    /// </summary>
    public object? MaxValue { get; set; }

    /// <summary>
    /// Default value for new rules
    /// </summary>
    public object? DefaultValue { get; set; }

    /// <summary>
    /// Allowed values for enum types
    /// </summary>
    public string[] AllowedValues { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Validation expression (C# expression syntax)
    /// </summary>
    public string? ValidationLogic { get; set; }

    /// <summary>
    /// Whether this rule has compliance implications
    /// </summary>
    public bool RequiresCompliance { get; set; }

    /// <summary>
    /// Minimum subscription tier required to use this rule
    /// </summary>
    public SubscriptionTier MinimumTier { get; set; } = SubscriptionTier.Starter;

    /// <summary>
    /// Features required to use this rule
    /// </summary>
    public string[] RequiredFeatures { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Whether this template is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// When this template was created
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Who created this template
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// Template version for tracking changes
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// When this template was last updated
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>
    /// Who last updated this template
    /// </summary>
    public string? UpdatedBy { get; set; }

    /// <summary>
    /// Usage statistics for this template
    /// </summary>
    public RuleTemplateUsageStats UsageStats { get; set; } = new();

    /// <summary>
    /// Regulatory maximum value (if applicable)
    /// </summary>
    public object? RegulatoryMax { get; set; }

    /// <summary>
    /// Currency for amount-based rules
    /// </summary>
    public string? Currency { get; set; }

    /// <summary>
    /// Risk level associated with this rule
    /// </summary>
    public PermissionRiskLevel RiskLevel { get; set; } = PermissionRiskLevel.Medium;
}

/// <summary>
/// Usage statistics for rule templates
/// </summary>
public class RuleTemplateUsageStats
{
    /// <summary>
    /// Number of tenants using this template
    /// </summary>
    public int TenantsUsing { get; set; } = 0;

    /// <summary>
    /// Total number of rules created from this template
    /// </summary>
    public int TotalRulesCreated { get; set; } = 0;

    /// <summary>
    /// Total number of roles using this template
    /// </summary>
    public int TotalRoles { get; set; } = 0;

    /// <summary>
    /// Average value configured across all uses
    /// </summary>
    public double AverageValue { get; set; } = 0;

    /// <summary>
    /// Compliance percentage for rules using this template
    /// </summary>
    public double ComplianceRate { get; set; } = 100;

    /// <summary>
    /// When statistics were last calculated
    /// </summary>
    public DateTimeOffset LastCalculatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// When this template was last used
    /// </summary>
    public DateTimeOffset? LastUsed { get; set; }

    /// <summary>
    /// Usage trend indicator
    /// </summary>
    public string UsageTrend { get; set; } = "stable";
}

/// <summary>
/// Request model for creating a new rule
/// </summary>
public class CreateRuleRequest
{
    /// <summary>
    /// Type of rule to create
    /// </summary>
    [Required(ErrorMessage = "Rule type is required")]
    public string RuleType { get; set; } = string.Empty;

    /// <summary>
    /// Value for the rule
    /// </summary>
    [Required(ErrorMessage = "Rule value is required")]
    public string RuleValue { get; set; } = string.Empty;

    /// <summary>
    /// Optional conditions for the rule
    /// </summary>
    public RuleCondition[] Conditions { get; set; } = Array.Empty<RuleCondition>();

    /// <summary>
    /// Optional notes about this rule
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Template ID if creating from template
    /// </summary>
    public string? TemplateId { get; set; }
}

/// <summary>
/// Request model for updating a rule
/// </summary>
public class UpdateRuleRequest
{
    /// <summary>
    /// Updated rule value
    /// </summary>
    public string? RuleValue { get; set; }

    /// <summary>
    /// Updated conditions
    /// </summary>
    public RuleCondition[]? Conditions { get; set; }

    /// <summary>
    /// Whether the rule is active
    /// </summary>
    public bool? IsActive { get; set; }

    /// <summary>
    /// Updated notes
    /// </summary>
    public string? Notes { get; set; }
}

/// <summary>
/// Result of rule evaluation
/// </summary>
public class RuleEvaluationResult
{
    /// <summary>
    /// Whether the rule allows the action
    /// </summary>
    public bool IsAllowed { get; set; }

    /// <summary>
    /// Reason for the result
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Rule that was evaluated
    /// </summary>
    public string RuleType { get; set; } = string.Empty;

    /// <summary>
    /// Value that was evaluated against
    /// </summary>
    public object? EvaluatedValue { get; set; }

    /// <summary>
    /// Rule value that was used
    /// </summary>
    public string RuleValue { get; set; } = string.Empty;

    /// <summary>
    /// Additional context information
    /// </summary>
    public Dictionary<string, object> Context { get; set; } = new();

    /// <summary>
    /// Time taken to evaluate the rule in milliseconds
    /// </summary>
    public int EvaluationTimeMs { get; set; }

    /// <summary>
    /// Additional arbitrary data attached to the evaluation (e.g. metrics)
    /// </summary>
    public Dictionary<string, object> AdditionalData { get; set; } = new();

    /// <summary>
    /// Create a result indicating the rule allows the action
    /// </summary>
    public static RuleEvaluationResult Allow(string ruleType, object evaluatedValue, string ruleValue, string reason)
    {
        return new RuleEvaluationResult
        {
            IsAllowed = true,
            RuleType = ruleType,
            EvaluatedValue = evaluatedValue,
            RuleValue = ruleValue,
            Reason = reason
        };
    }

    /// <summary>
    /// Create a result indicating the rule denies the action
    /// </summary>
    public static RuleEvaluationResult Deny(string ruleType, object evaluatedValue, string ruleValue, string reason)
    {
        return new RuleEvaluationResult
        {
            IsAllowed = false,
            RuleType = ruleType,
            EvaluatedValue = evaluatedValue,
            RuleValue = ruleValue,
            Reason = reason
        };
    }

    /// <summary>
    /// Create a result indicating the rule is not applicable
    /// </summary>
    public static RuleEvaluationResult NotApplicable(string ruleType = "")
    {
        return new RuleEvaluationResult
        {
            IsAllowed = true,
            RuleType = ruleType,
            Reason = "Rule not applicable or not configured"
        };
    }

    /// <summary>
    /// Create a result indicating an unknown rule type
    /// </summary>
    public static RuleEvaluationResult UnknownRule(string ruleType = "")
    {
        return new RuleEvaluationResult
        {
            IsAllowed = false,
            RuleType = ruleType,
            Reason = "Unknown rule type"
        };
    }

    /// <summary>
    /// Create a result indicating an error during evaluation
    /// </summary>
    public static RuleEvaluationResult Error(string errorMessage)
    {
        return new RuleEvaluationResult
        {
            IsAllowed = false,
            RuleType = "error",
            Reason = errorMessage
        };
    }

    /// <summary>
    /// Create a result indicating an error during evaluation with specific rule type
    /// </summary>
    public static RuleEvaluationResult Error(string ruleType, string errorMessage)
    {
        return new RuleEvaluationResult
        {
            IsAllowed = false,
            RuleType = ruleType,
            Reason = $"Error evaluating rule: {errorMessage}"
        };
    }
}

/// <summary>
/// Request model for testing rule evaluation
/// </summary>
public class RuleTestRequest
{
    /// <summary>
    /// Test scenario information
    /// </summary>
    public RuleTestScenario Scenario { get; set; } = new();
}

/// <summary>
/// Test scenario for rule evaluation
/// </summary>
public class RuleTestScenario
{
    /// <summary>
    /// Scenario identifier
    /// </summary>
    public string ScenarioId { get; set; } = string.Empty;

    /// <summary>
    /// Rule type to test
    /// </summary>
    public string RuleType { get; set; } = string.Empty;

    /// <summary>
    /// Rule value to test with
    /// </summary>
    public string RuleValue { get; set; } = string.Empty;

    /// <summary>
    /// Tenant context for testing
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    /// Test cases to evaluate
    /// </summary>
    public RuleTestCase[] TestCases { get; set; } = Array.Empty<RuleTestCase>();

    /// <summary>
    /// Additional context for testing
    /// </summary>
    public Dictionary<string, object> Context { get; set; } = new();
}

/// <summary>
/// Individual test case for rule evaluation
/// </summary>
public class RuleTestCase
{
    /// <summary>
    /// Value to test against the rule
    /// </summary>
    public object ContextValue { get; set; } = new();

    /// <summary>
    /// Expected result (allowed/denied)
    /// </summary>
    public string ExpectedResult { get; set; } = string.Empty;

    /// <summary>
    /// Description of this test case
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Result of rule testing
/// </summary>
public class RuleTestResult
{
    /// <summary>
    /// Scenario identifier
    /// </summary>
    public string ScenarioId { get; set; } = string.Empty;

    /// <summary>
    /// Individual test results
    /// </summary>
    public List<RuleTestCaseResult> TestCases { get; set; } = new();

    /// <summary>
    /// Overall test result
    /// </summary>
    public string OverallResult { get; set; } = string.Empty;

    /// <summary>
    /// Total time taken to evaluate all tests in milliseconds
    /// </summary>
    public int TotalEvaluationTimeMs { get; set; }

    /// <summary>
    /// Error message if testing failed
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Result of an individual test case
/// </summary>
public class RuleTestCaseResult
{
    /// <summary>
    /// Value that was tested
    /// </summary>
    public object ContextValue { get; set; } = new();

    /// <summary>
    /// Expected result from the test case
    /// </summary>
    public string ExpectedResult { get; set; } = string.Empty;

    /// <summary>
    /// Actual result from rule evaluation
    /// </summary>
    public string ActualResult { get; set; } = string.Empty;

    /// <summary>
    /// Whether this test case passed
    /// </summary>
    public bool Passed { get; set; }

    /// <summary>
    /// Reason for the result
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Time taken to evaluate this test case in milliseconds
    /// </summary>
    public int EvaluationTimeMs { get; set; }
}

/// <summary>
/// Regulatory compliance check result
/// </summary>
public class RegulatoryComplianceCheck
{
    /// <summary>
    /// Name of the regulatory framework
    /// </summary>
    public string Framework { get; set; } = string.Empty;

    /// <summary>
    /// Whether the rule complies with this framework
    /// </summary>
    public bool Compliant { get; set; }

    /// <summary>
    /// Details about the compliance check
    /// </summary>
    public string Details { get; set; } = string.Empty;

    /// <summary>
    /// Required action if not compliant
    /// </summary>
    public string? RequiredAction { get; set; }
}


#region Additional Model Types

// Enums
public enum TenantType
{
    Bank,
    MicrofinanceInstitution,
    CreditUnion,
    SaccoSociety
}

public enum RuleConflictResolutionStrategy
{
    TakeMaximum,
    TakeMinimum,
    TakeFirst,
    TakeLast,
    RequireConsistency
}

// Request/Response Models
public class CreateRuleTemplateRequest
{
    public RuleTemplate Template { get; set; } = new();
    public string CreatedBy { get; set; } = string.Empty;
}

public class UpdateRuleTemplateRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public object? MinValue { get; set; }
    public object? MaxValue { get; set; }
    public string[]? AllowedValues { get; set; }
    public string? DefaultValue { get; set; }
    public string? ValidationLogic { get; set; }
    public bool? RequiresCompliance { get; set; }
    public SubscriptionTier? MinimumTier { get; set; }
    public bool? IsActive { get; set; }
    public string UpdatedBy { get; set; } = string.Empty;
}

public class AddRoleRuleRequest
{
    public string RuleType { get; set; } = string.Empty;
    public string RuleValue { get; set; } = string.Empty;
    public RuleValueType? ValueType { get; set; }
    public List<RuleCondition>? Conditions { get; set; }
    public bool? IsActive { get; set; }
}

public class UpdateRoleRuleRequest
{
    public string? RuleValue { get; set; }
    public List<RuleCondition>? Conditions { get; set; }
    public bool? IsActive { get; set; }
}

public class ValidateRuleRequest
{
    public string RuleType { get; set; } = string.Empty;
    public string RuleValue { get; set; } = string.Empty;
    public RuleValueType ValueType { get; set; }
    public List<RuleCondition>? Conditions { get; set; }
}

public class TestRuleRequest
{
    public RuleTestScenario Scenario { get; set; } = new();
}

public class EvaluateAuthorityRequest
{
    public string Operation { get; set; } = string.Empty;
    public object Context { get; set; } = new();
}

public class CategoryInfo
{
    public string Category { get; set; } = string.Empty;
    public int TemplateCount { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class ValidateRuleTemplateRequest
{
    public string? ValidationLogic { get; set; }
    public object? MinValue { get; set; }
    public object? MaxValue { get; set; }
    public string[]? AllowedValues { get; set; }
}

public class ComplianceScanRequest
{
    public Guid[]? TenantIds { get; set; }
    public string[]? RuleTypes { get; set; }
    public string? Severity { get; set; }
}

// Response Models
public class RoleRuleResponse
{
    public string RuleId { get; set; } = string.Empty;
    public string RoleId { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public string RuleType { get; set; } = string.Empty;
    public string RuleValue { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ValueType { get; set; } = string.Empty;
    public object[] Conditions { get; set; } = Array.Empty<object>();
    public object ValidationResults { get; set; } = new();
    public int EffectiveUsers { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsActive { get; set; }
}

public class RoleRulesListResponse
{
    public string RoleId { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public object[] Rules { get; set; } = Array.Empty<object>();
    public object Summary { get; set; } = new();
}

public class RuleTemplatesResponse
{
    public object[] Templates { get; set; } = Array.Empty<object>();
    public CategoryInfo[] Categories { get; set; } = Array.Empty<CategoryInfo>();
    public int TotalAvailable { get; set; }
    public string TenantTier { get; set; } = string.Empty;
}

public class RuleValidationResponse
{
    public bool IsValid { get; set; }
    public string[] Errors { get; set; } = Array.Empty<string>();
    public string[] Warnings { get; set; } = Array.Empty<string>();
    public string ComplianceStatus { get; set; } = string.Empty;
    public string RuleType { get; set; } = string.Empty;
    public string RuleValue { get; set; } = string.Empty;
    public DateTime ValidatedAt { get; set; }
    public string[] Recommendations { get; set; } = Array.Empty<string>();
}

public class RuleTestResponse
{
    public string ScenarioId { get; set; } = string.Empty;
    public object[] TestResults { get; set; } = Array.Empty<object>();
    public string OverallResult { get; set; } = string.Empty;
    public int TotalTests { get; set; }
    public int PassedTests { get; set; }
    public int FailedTests { get; set; }
    public int TotalEvaluationTimeMs { get; set; }
    public object PerformanceMetrics { get; set; } = new();
    public DateTime TestedAt { get; set; }
}

public class ComplianceCheckResponse
{
    public Guid TenantId { get; set; }
    public bool IsCompliant { get; set; }
    public int ComplianceScore { get; set; }
    public int TotalRules { get; set; }
    public int CompliantRules { get; set; }
    public object[] Violations { get; set; } = Array.Empty<object>();
    public string[] Warnings { get; set; } = Array.Empty<string>();
    public DateTime CheckedAt { get; set; }
    public DateTime NextCheckDue { get; set; }
    public object RegulatoryRequirements { get; set; } = new();
}

public class RuleRecommendationsResponse
{
    public Guid TenantId { get; set; }
    public string TenantType { get; set; } = string.Empty;
    public string SubscriptionTier { get; set; } = string.Empty;
    public object[] Recommendations { get; set; } = Array.Empty<object>();
    public int TotalRecommendations { get; set; }
    public int RequiredRecommendations { get; set; }
    public int OptionalRecommendations { get; set; }
    public DateTime GeneratedAt { get; set; }
    public string Summary { get; set; } = string.Empty;
}

public class AuthorityEvaluationResponse
{
    public string Operation { get; set; } = string.Empty;
    public bool HasAuthority { get; set; }
    public string[] Reasons { get; set; } = Array.Empty<string>();
    public string[] RequiredActions { get; set; } = Array.Empty<string>();
    public Dictionary<string, string> UserRules { get; set; } = new();
    public DateTime EvaluatedAt { get; set; }
    public string[] RecommendedNextSteps { get; set; } = Array.Empty<string>();
}

public class AuthorityCheckResult
{
    public bool HasAuthority { get; set; }
    public List<string> Reasons { get; set; } = new();
    public List<string> RequiredActions { get; set; } = new();
}

public class ComplianceViolation
{
    public Guid TenantId { get; set; }
    public string RuleType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class UserRuleComplianceResult
{
    public string UserId { get; set; } = string.Empty;
    public Guid? TenantId { get; set; }
    public bool IsCompliant { get; set; }
    public int ComplianceScore { get; set; }
    public List<string> Violations { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public DateTime CheckedAt { get; set; }
}

public class UserRuleHierarchy
{
    public string UserId { get; set; } = string.Empty;
    public Guid? TenantId { get; set; }
    public Dictionary<string, Dictionary<string, string>> RoleRules { get; set; } = new();
    public List<RuleConflict> ConflictingRules { get; set; } = new();
    public Dictionary<string, string> EffectiveRules { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
}

public class RuleConflict
{
    public string RuleType { get; set; } = string.Empty;
    public string[] ConflictingRoles { get; set; } = Array.Empty<string>();
    public string[] ConflictingValues { get; set; } = Array.Empty<string>();
}

public class TenantRuleAnalysis
{
    public Guid TenantId { get; set; }
    public int TotalRules { get; set; }
    public int ActiveRules { get; set; }
    public double ComplianceScore { get; set; }
    public DateTime LastUpdated { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    public string[] RecommendedActions { get; set; } = Array.Empty<string>();
}

public class RuleTemplateFilter
{
    public string? Category { get; set; }
    public string? SearchTerm { get; set; }
    public RuleValueType? ValueType { get; set; }
    public bool? RequiresCompliance { get; set; }
    public SubscriptionTier? MinimumTier { get; set; }
    public bool? IsActive { get; set; }
}

public class RuleTemplateListResponse
{
    public RuleTemplate[] Templates { get; set; } = Array.Empty<RuleTemplate>();
    public CategoryInfo[] Categories { get; set; } = Array.Empty<CategoryInfo>();
    public int TotalCount { get; set; }
    public string? FilterInfo { get; set; }
}

public class RuleTemplateAnalytics
{
    public int TotalTemplates { get; set; }
    public int ActiveTemplates { get; set; }
    public int CategoriesCount { get; set; }
    public string MostUsedTemplate { get; set; } = string.Empty;
    public double OverallComplianceRate { get; set; }
    public DateTime GeneratedAt { get; set; }
}

public class RuleEngineDeploymentRequest
{
    public string InitiatedBy { get; set; } = string.Empty;
    public string[] RuleTypes { get; set; } = Array.Empty<string>();
    public Guid[]? TargetTenants { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class RuleEngineDeploymentResult
{
    public string DeploymentId { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<Guid> AffectedTenants { get; set; } = new();
    public int SuccessfulTenants { get; set; }
    public int FailedTenants { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class ComplianceScanResult
{
    public string ScanId { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int TotalTenants { get; set; }
    public int CompliantTenants { get; set; }
    public int ViolatingTenants { get; set; }
    public double ComplianceRate { get; set; }
    public List<ComplianceViolation> Violations { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class RuleRecommendation
{
    public string TemplateId { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int Priority { get; set; }
    public string RecommendedValue { get; set; } = string.Empty;
    public string Rationale { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public string ComplianceImpact { get; set; } = string.Empty;
}

public class RuleRecommendationResponse
{
    public TenantType TenantType { get; set; }
    public SubscriptionTier SubscriptionTier { get; set; }
    public RuleRecommendation[] Recommendations { get; set; } = Array.Empty<RuleRecommendation>();
    public DateTime GeneratedAt { get; set; }
    public string Summary { get; set; } = string.Empty;
}

public class RuleTemplateValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

// Platform-specific models
public class PlatformRuleTemplateListResponse
{
    public PlatformRuleTemplateDetail[] Templates { get; set; } = Array.Empty<PlatformRuleTemplateDetail>();
    public PlatformTemplateCategoryInfo[] Categories { get; set; } = Array.Empty<PlatformTemplateCategoryInfo>();
    public object Pagination { get; set; } = new();
    public object Statistics { get; set; } = new();
    public object FilterInfo { get; set; } = new();
}

public class PlatformRuleTemplateDetail
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public RuleValueType ValueType { get; set; }
    public object? MinValue { get; set; }
    public object? MaxValue { get; set; }
    public string[]? AllowedValues { get; set; }
    public string? DefaultValue { get; set; }
    public string? ValidationLogic { get; set; }
    public bool RequiresCompliance { get; set; }
    public SubscriptionTier MinimumTier { get; set; }
    public bool IsActive { get; set; }
    public string Version { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public object UsageStats { get; set; } = new();
}

public class PlatformTemplateCategoryInfo
{
    public string Category { get; set; } = string.Empty;
    public int TemplateCount { get; set; }
    public string Description { get; set; } = string.Empty;
    public double AverageUsage { get; set; }
    public double ComplianceRate { get; set; }
}

public class RuleTemplateValidationResponse
{
    public string TemplateId { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public string[] Errors { get; set; } = Array.Empty<string>();
    public string[] Warnings { get; set; } = Array.Empty<string>();
    public object ValidationDetails { get; set; } = new();
    public string[] Recommendations { get; set; } = Array.Empty<string>();
    public DateTime ValidatedAt { get; set; }
}

public class PlatformRuleTemplateUsageResponse
{
    public string TemplateId { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public object UsageStatistics { get; set; } = new();
    public object[] TenantBreakdown { get; set; } = Array.Empty<object>();
    public object ValueDistribution { get; set; } = new();
    public object ComplianceMetrics { get; set; } = new();
    public object PerformanceMetrics { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
}

public class PlatformRuleTemplateAnalyticsResponse
{
    public object Summary { get; set; } = new();
    public object Usage { get; set; } = new();
    public object Compliance { get; set; } = new();
    public object Performance { get; set; } = new();
    public object Trends { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
}

public class TenantRuleAnalysisResponse
{
    public Guid TenantId { get; set; }
    public object Analysis { get; set; } = new();
    public object RuleBreakdown { get; set; } = new();
    public object ComplianceStatus { get; set; } = new();
    public object Recommendations { get; set; } = new();
    public object BenchmarkComparison { get; set; } = new();
    public object TrendAnalysis { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
}

public class ComplianceScanResponse
{
    public string ScanId { get; set; } = string.Empty;
    public object ExecutionSummary { get; set; } = new();
    public object ComplianceMetrics { get; set; } = new();
    public object ViolationSummary { get; set; } = new();
    public object[] Violations { get; set; } = Array.Empty<object>();
    public object TrendComparison { get; set; } = new();
    public object RegulatoryContext { get; set; } = new();
    public string[] RecommendedActions { get; set; } = Array.Empty<string>();
    public DateTime NextScanSchedule { get; set; }
}

public class RuleEngineHealthResponse
{
    public string OverallStatus { get; set; } = string.Empty;
    public object SystemMetrics { get; set; } = new();
    public object PerformanceMetrics { get; set; } = new();
    public object ComplianceMetrics { get; set; } = new();
    public object ResourceUtilization { get; set; } = new();
    public object RecentActivity { get; set; } = new();
    public object[] Alerts { get; set; } = Array.Empty<object>();
    public DateTime CheckedAt { get; set; }
}

public class PlatformAnalyticsDashboardResponse
{
    public object TimeRange { get; set; } = new();
    public object Usage { get; set; } = new();
    public object Performance { get; set; } = new();
    public object Compliance { get; set; } = new();
    public object TenantInsights { get; set; } = new();
    public string[] Recommendations { get; set; } = Array.Empty<string>();
    public DateTime GeneratedAt { get; set; }
}

#endregion


// --- Appended Models for AuthorizationController ---

public class PermissionCheckResponse
{
    public string UserId { get; set; } = string.Empty;
    public string Permission { get; set; } = string.Empty;
    public bool HasPermission { get; set; }
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    public string CheckedBy { get; set; } = string.Empty;
}

public class BulkPermissionCheckRequest
{
    // Use string[] to match service signatures that expect arrays
    public string[] Permissions { get; set; } = Array.Empty<string>();
}

public class BulkPermissionCheckResponse
{
    public string UserId { get; set; } = string.Empty;
    public Dictionary<string, bool> Results { get; set; } = new();
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    public string CheckedBy { get; set; } = string.Empty;
}

public class UserPermissionsResponse
{
    public string UserId { get; set; } = string.Empty;
    public IEnumerable<string> Permissions { get; set; } = Array.Empty<string>();
    public DateTime RetrievedAt { get; set; } = DateTime.UtcNow;
    public string RetrievedBy { get; set; } = string.Empty;
}

public class UserRulesResponse
{
    public string UserId { get; set; } = string.Empty;
    public Dictionary<string, string> Rules { get; set; } = new();
    public DateTime RetrievedAt { get; set; } = DateTime.UtcNow;
    public string RetrievedBy { get; set; } = string.Empty;
}

public class RuleEvaluationRequest
{
    public object Value { get; set; } = new();
}

public class RuleEvaluationResponse
{
    public string UserId { get; set; } = string.Empty;
    public string RuleType { get; set; } = string.Empty;
    public object EvaluatedValue { get; set; } = new();
    public RuleEvaluationResult Result { get; set; } = new();
    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;
    public string EvaluatedBy { get; set; } = string.Empty;
}

public class ActionValidationRequest
{
    public Dictionary<string, object> Context { get; set; } = new();
}

public class ActionValidationResult
{
    public bool IsAuthorized { get; set; }
    public string Reason { get; set; } = string.Empty;

    // Map of failed rule -> reason
    public Dictionary<string, string> FailedRules { get; set; } = new();

    // Optional: the evaluated item (action/permission) from AuthorizationResult
    public string? EvaluatedItem { get; set; }

    // Optional: required permission for the action
    public string? RequiredPermission { get; set; }

    // Detailed rule evaluation results
    public RuleEvaluationResult[] RuleResults { get; set; } = Array.Empty<RuleEvaluationResult>();
}

public class ActionValidationResponse
{
    public string UserId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public Dictionary<string, object> Context { get; set; } = new();
    public ActionValidationResult Result { get; set; } = new();
    public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;
    public string ValidatedBy { get; set; } = string.Empty;
}

// Replace minimal RuleValidationResult with the fuller validation model used by services/controllers
public class RuleValidationResult
{
    /// <summary>
    /// Whether the rule configuration is valid
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Validation errors
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Validation warnings
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Optional message or diagnostic
    /// </summary>
    public string? Message { get; set; }
}
