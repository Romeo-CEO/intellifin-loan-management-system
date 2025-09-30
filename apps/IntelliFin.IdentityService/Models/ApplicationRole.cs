using Microsoft.AspNetCore.Identity;

namespace IntelliFin.IdentityService.Models;

/// <summary>
/// Extended IdentityRole for multi-tenant role management
/// Supports custom tenant roles and role templates
/// </summary>
public class ApplicationRole : IdentityRole
{
    /// <summary>
    /// Tenant ID for role scoping
    /// NULL for platform roles, Guid for tenant roles
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    /// Detailed description of the role's purpose and responsibilities
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// When this role was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Who created this role (User ID)
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// When this role was last modified
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Who last updated this role (User ID)
    /// </summary>
    public string? UpdatedBy { get; set; }

    /// <summary>
    /// Whether this is a custom tenant role (true) or system/template role (false)
    /// </summary>
    public bool IsCustom { get; set; } = true;

    /// <summary>
    /// Role category for organizational grouping
    /// </summary>
    public RoleCategory Category { get; set; } = RoleCategory.Operations;

    /// <summary>
    /// Denormalized count of users assigned to this role
    /// Updated when user-role assignments change
    /// </summary>
    public int UserCount { get; set; } = 0;

    /// <summary>
    /// Whether this role is active and can be assigned
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Template ID if this role was created from a template
    /// </summary>
    public string? TemplateId { get; set; }

    /// <summary>
    /// Compliance score (0-100) based on BoZ requirements
    /// Calculated during validation
    /// </summary>
    public int ComplianceScore { get; set; } = 0;

    /// <summary>
    /// Last compliance validation date
    /// </summary>
    public DateTime? LastValidatedAt { get; set; }

    /// <summary>
    /// Navigation property for role claims (permissions)
    /// Uses built-in ASP.NET Identity claims system
    /// </summary>
    public virtual ICollection<IdentityRoleClaim<string>> Claims { get; set; } = new List<IdentityRoleClaim<string>>();

    /// <summary>
    /// Navigation property for user role assignments
    /// Uses built-in ASP.NET Identity user roles system
    /// </summary>
    public virtual ICollection<IdentityUserRole<string>> UserRoles { get; set; } = new List<IdentityUserRole<string>>();
}

/// <summary>
/// Categories for organizing roles within tenant organizations
/// Aligned with typical financial institution structures
/// </summary>
public enum RoleCategory
{
    /// <summary>
    /// Front-line loan processing staff
    /// </summary>
    LoanOfficers = 1,

    /// <summary>
    /// Credit analysis and approval roles
    /// </summary>
    CreditManagement = 2,

    /// <summary>
    /// Financial operations and accounting
    /// </summary>
    Finance = 3,

    /// <summary>
    /// General operational support
    /// </summary>
    Operations = 4,

    /// <summary>
    /// Compliance and risk management
    /// </summary>
    Compliance = 5,

    /// <summary>
    /// Management and supervisory roles
    /// </summary>
    Management = 6,

    /// <summary>
    /// System and tenant administration
    /// </summary>
    Administration = 7
}

/// <summary>
/// Extended metadata for role-permission assignments
/// Provides audit trail beyond basic IdentityRoleClaim
/// </summary>
public class RolePermissionAssignment
{
    public int Id { get; set; }

    /// <summary>
    /// Role this permission is assigned to
    /// </summary>
    public string RoleId { get; set; } = string.Empty;

    /// <summary>
    /// Permission identifier (e.g., "clients:view")
    /// </summary>
    public string Permission { get; set; } = string.Empty;

    /// <summary>
    /// When this permission was assigned to the role
    /// </summary>
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Who assigned this permission (User ID)
    /// </summary>
    public string AssignedBy { get; set; } = string.Empty;

    /// <summary>
    /// Whether this permission assignment is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Optional notes about why this permission was assigned
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Navigation property to the role
    /// </summary>
    public virtual ApplicationRole Role { get; set; } = null!;
}