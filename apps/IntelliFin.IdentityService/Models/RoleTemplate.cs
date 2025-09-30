namespace IntelliFin.IdentityService.Models;

/// <summary>
/// Platform-provided role templates for quick tenant role setup
/// Created by IntelliFin team to guide tenant role composition
/// </summary>
public class RoleTemplate
{
    /// <summary>
    /// Unique template identifier
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Template name (e.g., "Loan Officer (Standard)")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Detailed description of the role's purpose and typical responsibilities
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Role category for organizational grouping
    /// </summary>
    public RoleCategory Category { get; set; } = RoleCategory.Operations;

    /// <summary>
    /// Permissions typically included in this role
    /// Tenants can modify these during role creation
    /// </summary>
    public string[] RecommendedPermissions { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Mandatory permissions for BoZ compliance
    /// Cannot be removed from roles based on this template
    /// </summary>
    public string[] RequiredPermissions { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Permissions that should NOT be included (segregation of duties)
    /// </summary>
    public string[] ProhibitedPermissions { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Minimum subscription tier required to use this template
    /// </summary>
    public SubscriptionTier MinimumTier { get; set; } = SubscriptionTier.Starter;

    /// <summary>
    /// Required features for this template to be applicable
    /// </summary>
    public string[] RequiredFeatures { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Compliance frameworks this template addresses
    /// </summary>
    public ComplianceFramework[] ApplicableFrameworks { get; set; } = Array.Empty<ComplianceFramework>();

    /// <summary>
    /// Whether this template is currently active and available to tenants
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Version of this template for tracking updates
    /// </summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// When this template was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Who created this template (Platform Admin User ID)
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// When this template was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Who last updated this template (Platform Admin User ID)
    /// </summary>
    public string? UpdatedBy { get; set; }

    /// <summary>
    /// Usage statistics across tenants
    /// </summary>
    public RoleTemplateUsageStats UsageStats { get; set; } = new();

    /// <summary>
    /// Additional guidance notes for tenants using this template
    /// </summary>
    public string? ComplianceNotes { get; set; }

    /// <summary>
    /// Tags for template categorization and search
    /// </summary>
    public string[] Tags { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Usage statistics for role templates
/// </summary>
public class RoleTemplateUsageStats
{
    /// <summary>
    /// Number of tenants currently using this template
    /// </summary>
    public int TenantsUsing { get; set; } = 0;

    /// <summary>
    /// Total number of roles created from this template
    /// </summary>
    public int TotalRolesCreated { get; set; } = 0;

    /// <summary>
    /// Average number of users assigned to roles based on this template
    /// </summary>
    public double AverageUserCount { get; set; } = 0;

    /// <summary>
    /// When usage statistics were last calculated
    /// </summary>
    public DateTime LastCalculatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Compliance frameworks supported by role templates
/// </summary>
public enum ComplianceFramework
{
    /// <summary>
    /// Bank of Zambia regulatory requirements
    /// </summary>
    BoZ = 1,

    /// <summary>
    /// International Financial Reporting Standards
    /// </summary>
    IFRS = 2,

    /// <summary>
    /// Anti-Money Laundering regulations
    /// </summary>
    AML = 3,

    /// <summary>
    /// Know Your Customer requirements
    /// </summary>
    KYC = 4,

    /// <summary>
    /// Internal audit and control standards
    /// </summary>
    InternalAudit = 5,

    /// <summary>
    /// Credit risk management standards
    /// </summary>
    CreditRisk = 6
}

/// <summary>
/// Request model for creating a role from a template
/// </summary>
public class CreateRoleFromTemplateRequest
{
    /// <summary>
    /// Template ID to base the role on
    /// </summary>
    public string TemplateId { get; set; } = string.Empty;

    /// <summary>
    /// Custom name for the new role
    /// </summary>
    public string RoleName { get; set; } = string.Empty;

    /// <summary>
    /// Custom description for the new role
    /// </summary>
    public string? RoleDescription { get; set; }

    /// <summary>
    /// Modifications to the template's recommended permissions
    /// </summary>
    public TemplatePermissionModifications? PermissionModifications { get; set; }

    /// <summary>
    /// Whether to include all recommended permissions from template
    /// </summary>
    public bool IncludeAllRecommended { get; set; } = true;
}

/// <summary>
/// Modifications to template permissions during role creation
/// </summary>
public class TemplatePermissionModifications
{
    /// <summary>
    /// Additional permissions to add beyond template recommendations
    /// </summary>
    public string[] AdditionalPermissions { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Recommended permissions to exclude from the role
    /// Cannot exclude required permissions
    /// </summary>
    public string[] ExcludedPermissions { get; set; } = Array.Empty<string>();
}