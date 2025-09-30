using IntelliFin.IdentityService.Models;

namespace IntelliFin.IdentityService.Services;

/// <summary>
/// Service for managing platform role templates
/// Available to Platform Plane for template management
/// </summary>
public interface IRoleTemplateService
{
    /// <summary>
    /// Create a new role template (Platform Plane only)
    /// </summary>
    Task<RoleTemplate> CreateTemplateAsync(
        RoleTemplate template,
        string createdBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update an existing role template (Platform Plane only)
    /// </summary>
    Task<RoleTemplate> UpdateTemplateAsync(
        string templateId,
        RoleTemplate template,
        string updatedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a role template (Platform Plane only)
    /// </summary>
    Task<bool> DeleteTemplateAsync(
        string templateId,
        string deletedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all available role templates
    /// </summary>
    Task<RoleTemplate[]> GetAllTemplatesAsync(
        RoleCategory? category = null,
        SubscriptionTier? minimumTier = null,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get role templates available to a specific tenant
    /// Filtered by subscription tier and enabled features
    /// </summary>
    Task<RoleTemplate[]> GetTemplatesForTenantAsync(
        string tenantId,
        RoleCategory? category = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a specific role template by ID
    /// </summary>
    Task<RoleTemplate?> GetTemplateByIdAsync(
        string templateId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Search role templates
    /// </summary>
    Task<RoleTemplate[]> SearchTemplatesAsync(
        string query,
        RoleCategory? category = null,
        SubscriptionTier? minimumTier = null,
        int maxResults = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get role templates by category
    /// </summary>
    Task<RoleTemplate[]> GetTemplatesByCategoryAsync(
        RoleCategory category,
        SubscriptionTier? minimumTier = null,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get template categories with statistics
    /// </summary>
    Task<RoleTemplateCategorySummary[]> GetTemplateCategoriesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update template usage statistics
    /// Called when tenants create roles from templates
    /// </summary>
    Task UpdateTemplateUsageAsync(
        string templateId,
        string tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get template usage analytics (Platform Plane only)
    /// </summary>
    Task<TemplateUsageAnalytics[]> GetTemplateUsageAnalyticsAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate template permissions against current permission catalog
    /// </summary>
    Task<TemplateValidationResult> ValidateTemplateAsync(
        string templateId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get recommended templates for a tenant based on their current roles
    /// </summary>
    Task<RoleTemplate[]> GetRecommendedTemplatesAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyze how well a template matches a tenant's existing role structure
    /// </summary>
    Task<TemplateMatchAnalysis> AnalyzeTemplateMatchAsync(
        string templateId,
        string tenantId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Summary information for role template categories
/// </summary>
public class RoleTemplateCategorySummary
{
    /// <summary>
    /// Category name
    /// </summary>
    public RoleCategory Category { get; set; }

    /// <summary>
    /// Number of templates in this category
    /// </summary>
    public int TemplateCount { get; set; }

    /// <summary>
    /// Description of roles in this category
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Most popular template in this category
    /// </summary>
    public string? MostPopularTemplate { get; set; }

    /// <summary>
    /// Average usage count across templates in category
    /// </summary>
    public double AverageUsage { get; set; }
}

/// <summary>
/// Usage analytics for role templates
/// </summary>
public class TemplateUsageAnalytics
{
    /// <summary>
    /// Template ID
    /// </summary>
    public string TemplateId { get; set; } = string.Empty;

    /// <summary>
    /// Template name
    /// </summary>
    public string TemplateName { get; set; } = string.Empty;

    /// <summary>
    /// Total usage count
    /// </summary>
    public int TotalUsage { get; set; }

    /// <summary>
    /// Number of unique tenants using this template
    /// </summary>
    public int UniqueTenants { get; set; }

    /// <summary>
    /// Usage trend over time
    /// </summary>
    public TemplateUsageTrend[] UsageTrend { get; set; } = Array.Empty<TemplateUsageTrend>();

    /// <summary>
    /// Most common modifications made by tenants
    /// </summary>
    public TemplateModificationStats[] CommonModifications { get; set; } = Array.Empty<TemplateModificationStats>();

    /// <summary>
    /// Average user count for roles created from this template
    /// </summary>
    public double AverageRoleUserCount { get; set; }

    /// <summary>
    /// Template rating based on usage and feedback
    /// </summary>
    public double Rating { get; set; }
}

/// <summary>
/// Usage trend data point for a template
/// </summary>
public class TemplateUsageTrend
{
    /// <summary>
    /// Date of usage
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Number of roles created from template on this date
    /// </summary>
    public int RolesCreated { get; set; }

    /// <summary>
    /// Number of unique tenants using template on this date
    /// </summary>
    public int UniqueTenants { get; set; }
}

/// <summary>
/// Statistics about common template modifications
/// </summary>
public class TemplateModificationStats
{
    /// <summary>
    /// Type of modification (e.g., "permission_added", "permission_removed")
    /// </summary>
    public string ModificationType { get; set; } = string.Empty;

    /// <summary>
    /// Specific permission involved (if applicable)
    /// </summary>
    public string? Permission { get; set; }

    /// <summary>
    /// Number of tenants making this modification
    /// </summary>
    public int TenantCount { get; set; }

    /// <summary>
    /// Percentage of template users making this modification
    /// </summary>
    public double Percentage { get; set; }
}

/// <summary>
/// Validation result for a role template
/// </summary>
public class TemplateValidationResult
{
    /// <summary>
    /// Template ID that was validated
    /// </summary>
    public string TemplateId { get; set; } = string.Empty;

    /// <summary>
    /// Whether the template is valid
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Issues found during validation
    /// </summary>
    public TemplateValidationIssue[] Issues { get; set; } = Array.Empty<TemplateValidationIssue>();

    /// <summary>
    /// Warnings about the template
    /// </summary>
    public string[] Warnings { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Recommendations for template improvement
    /// </summary>
    public string[] Recommendations { get; set; } = Array.Empty<string>();

    /// <summary>
    /// When this validation was performed
    /// </summary>
    public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Validation issue found in a template
/// </summary>
public class TemplateValidationIssue
{
    /// <summary>
    /// Severity of the issue
    /// </summary>
    public IssueSeverity Severity { get; set; }

    /// <summary>
    /// Type of issue
    /// </summary>
    public string IssueType { get; set; } = string.Empty;

    /// <summary>
    /// Description of the issue
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Permission involved in the issue (if applicable)
    /// </summary>
    public string? Permission { get; set; }

    /// <summary>
    /// Recommended action to resolve the issue
    /// </summary>
    public string RecommendedAction { get; set; } = string.Empty;
}

/// <summary>
/// Analysis of how well a template matches a tenant's role structure
/// </summary>
public class TemplateMatchAnalysis
{
    /// <summary>
    /// Template ID being analyzed
    /// </summary>
    public string TemplateId { get; set; } = string.Empty;

    /// <summary>
    /// Tenant ID being analyzed
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Overall match score (0-100)
    /// </summary>
    public int MatchScore { get; set; }

    /// <summary>
    /// Whether the tenant already has similar roles
    /// </summary>
    public bool HasSimilarRoles { get; set; }

    /// <summary>
    /// Existing roles that are similar to this template
    /// </summary>
    public SimilarRole[] SimilarRoles { get; set; } = Array.Empty<SimilarRole>();

    /// <summary>
    /// Permissions in template that tenant doesn't currently use
    /// </summary>
    public string[] NewPermissions { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Permissions tenant uses that aren't in template
    /// </summary>
    public string[] MissingPermissions { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Estimated benefit of using this template
    /// </summary>
    public TemplateBenefitAssessment BenefitAssessment { get; set; } = new();

    /// <summary>
    /// Potential issues with adopting this template
    /// </summary>
    public string[] PotentialIssues { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Assessment of benefits from using a template
/// </summary>
public class TemplateBenefitAssessment
{
    /// <summary>
    /// Whether this template would improve compliance
    /// </summary>
    public bool ImprovesCompliance { get; set; }

    /// <summary>
    /// Whether this template reduces role complexity
    /// </summary>
    public bool ReducesComplexity { get; set; }

    /// <summary>
    /// Whether this template provides missing capabilities
    /// </summary>
    public bool ProvidesMissingCapabilities { get; set; }

    /// <summary>
    /// Estimated compliance score improvement
    /// </summary>
    public int ComplianceImprovement { get; set; }

    /// <summary>
    /// Specific benefits this template would provide
    /// </summary>
    public string[] Benefits { get; set; } = Array.Empty<string>();
}