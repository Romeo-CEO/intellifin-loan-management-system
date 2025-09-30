using IntelliFin.IdentityService.Models;

namespace IntelliFin.IdentityService.Services;

/// <summary>
/// Service for managing rule templates (Platform Plane only)
/// </summary>
public interface IRuleTemplateService
{
    /// <summary>
    /// Creates a new rule template
    /// </summary>
    Task<RuleTemplate> CreateRuleTemplateAsync(CreateRuleTemplateRequest request);

    /// <summary>
    /// Updates an existing rule template
    /// </summary>
    Task<RuleTemplate> UpdateRuleTemplateAsync(string templateId, UpdateRuleTemplateRequest request);

    /// <summary>
    /// Gets a specific rule template
    /// </summary>
    Task<RuleTemplate?> GetRuleTemplateAsync(string templateId);

    /// <summary>
    /// Gets all available rule templates with optional filtering
    /// </summary>
    Task<RuleTemplateListResponse> GetRuleTemplatesAsync(RuleTemplateFilter? filter = null);

    /// <summary>
    /// Deletes a rule template
    /// </summary>
    Task<bool> DeleteRuleTemplateAsync(string templateId);

    /// <summary>
    /// Validates rule template configuration
    /// </summary>
    Task<RuleTemplateValidationResult> ValidateRuleTemplateAsync(RuleTemplate template);

    /// <summary>
    /// Gets rule templates by category
    /// </summary>
    Task<Dictionary<string, RuleTemplate[]>> GetRuleTemplatesByCategoryAsync();

    /// <summary>
    /// Gets usage statistics for rule templates across all tenants
    /// </summary>
    Task<RuleTemplateUsageStats> GetTemplateUsageStatsAsync(string templateId);

    /// <summary>
    /// Gets aggregated analytics for all rule templates
    /// </summary>
    Task<RuleTemplateAnalytics> GetTemplateAnalyticsAsync();

    /// <summary>
    /// Deploys rule engine updates to all tenants
    /// </summary>
    Task<RuleEngineDeploymentResult> DeployRuleEngineUpdatesAsync(RuleEngineDeploymentRequest request);

    /// <summary>
    /// Analyzes rule usage for a specific tenant
    /// </summary>
    Task<TenantRuleAnalysis> AnalyzeTenantRuleUsageAsync(Guid tenantId);

    /// <summary>
    /// Performs compliance scan across all tenant rules
    /// </summary>
    Task<ComplianceScanResult> PerformComplianceScanAsync(ComplianceScanRequest? request = null);

    /// <summary>
    /// Validates template business logic
    /// </summary>
    Task<RuleTemplateValidationResult> ValidateTemplateLogicAsync(string templateId, string validationLogic);

    /// <summary>
    /// Gets recommended rule configurations for tenant type
    /// </summary>
    Task<RuleRecommendationResponse> GetRuleRecommendationsAsync(TenantType tenantType, SubscriptionTier tier);
}