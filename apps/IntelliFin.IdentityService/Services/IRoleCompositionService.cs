using IntelliFin.IdentityService.Models;

namespace IntelliFin.IdentityService.Services;

/// <summary>
/// Service for managing tenant role composition and permission assignments
/// Handles the "Lego brick" role building system
/// </summary>
public interface IRoleCompositionService
{
    /// <summary>
    /// Create a new custom role for a tenant
    /// </summary>
    Task<ApplicationRole> CreateRoleAsync(
        CreateRoleRequest request,
        string tenantId,
        string createdBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update role metadata (name, description, category)
    /// </summary>
    Task<ApplicationRole> UpdateRoleAsync(
        string roleId,
        UpdateRoleRequest request,
        string tenantId,
        string updatedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a role (with validation for user assignments)
    /// </summary>
    Task<bool> DeleteRoleAsync(
        string roleId,
        string tenantId,
        string deletedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all roles for a tenant with permission summaries
    /// </summary>
    Task<ApplicationRole[]> GetTenantRolesAsync(
        string tenantId,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get detailed role information including all permissions
    /// </summary>
    Task<ApplicationRole?> GetRoleByIdAsync(
        string roleId,
        string tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Add permissions to a role with validation
    /// </summary>
    Task<RolePermissionResult> AddPermissionsToRoleAsync(
        string roleId,
        AddPermissionsToRoleRequest request,
        string tenantId,
        string assignedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove a specific permission from a role
    /// </summary>
    Task<bool> RemovePermissionFromRoleAsync(
        string roleId,
        string permissionId,
        string tenantId,
        string removedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk update role permissions (replaces all existing permissions)
    /// </summary>
    Task<RolePermissionResult> BulkUpdateRolePermissionsAsync(
        string roleId,
        BulkUpdateRolePermissionsRequest request,
        string tenantId,
        string updatedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all permissions assigned to a role
    /// </summary>
    Task<string[]> GetRolePermissionsAsync(
        string roleId,
        string tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate role for compliance and segregation of duties
    /// </summary>
    Task<ComplianceValidationResult> ValidateRoleComplianceAsync(
        string roleId,
        string tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check all tenant roles against BoZ compliance requirements
    /// </summary>
    Task<ComplianceValidationResult[]> CheckTenantRoleComplianceAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Preview role capabilities and potential issues before assignment
    /// </summary>
    Task<RolePreviewResult> PreviewRoleAsync(
        string roleId,
        string tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get role summary and context information for tenant
    /// </summary>
    Task<TenantRoleSummary> GetTenantRoleSummaryAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a role from a platform template
    /// </summary>
    Task<ApplicationRole> CreateRoleFromTemplateAsync(
        CreateRoleFromTemplateRequest request,
        string tenantId,
        string createdBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update user count for a role (called when user assignments change)
    /// </summary>
    Task UpdateRoleUserCountAsync(
        string roleId,
        int newCount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Search roles within a tenant
    /// </summary>
    Task<ApplicationRole[]> SearchTenantRolesAsync(
        string tenantId,
        string query,
        RoleCategory? category = null,
        int maxResults = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get roles by category for a tenant
    /// </summary>
    Task<ApplicationRole[]> GetRolesByCategoryAsync(
        string tenantId,
        RoleCategory category,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result for role preview operations
/// </summary>
public class RolePreviewResult
{
    /// <summary>
    /// Role ID being previewed
    /// </summary>
    public string RoleId { get; set; } = string.Empty;

    /// <summary>
    /// Role name
    /// </summary>
    public string RoleName { get; set; } = string.Empty;

    /// <summary>
    /// Permissions assigned to this role
    /// </summary>
    public string[] Permissions { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Capabilities this role would grant
    /// </summary>
    public RoleCapability[] Capabilities { get; set; } = Array.Empty<RoleCapability>();

    /// <summary>
    /// Compliance validation results
    /// </summary>
    public ComplianceValidationResult ComplianceResults { get; set; } = new();

    /// <summary>
    /// Risk assessment for this role
    /// </summary>
    public RoleRiskAssessment RiskAssessment { get; set; } = new();

    /// <summary>
    /// Similar roles in the tenant for comparison
    /// </summary>
    public SimilarRole[] SimilarRoles { get; set; } = Array.Empty<SimilarRole>();
}

/// <summary>
/// Capability granted by a role
/// </summary>
public class RoleCapability
{
    /// <summary>
    /// Area of functionality (e.g., "Client Management")
    /// </summary>
    public string Area { get; set; } = string.Empty;

    /// <summary>
    /// Specific actions possible (e.g., "View, Create, Edit client information")
    /// </summary>
    public string[] Actions { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Risk level of this capability
    /// </summary>
    public PermissionRiskLevel RiskLevel { get; set; }
}

/// <summary>
/// Risk assessment for a role
/// </summary>
public class RoleRiskAssessment
{
    /// <summary>
    /// Overall risk score (0-100)
    /// </summary>
    public int OverallRiskScore { get; set; }

    /// <summary>
    /// Risk level classification
    /// </summary>
    public PermissionRiskLevel RiskLevel { get; set; }

    /// <summary>
    /// High-risk permissions in this role
    /// </summary>
    public string[] HighRiskPermissions { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Risk factors identified
    /// </summary>
    public string[] RiskFactors { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Recommended mitigations
    /// </summary>
    public string[] Mitigations { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Similar role for comparison during preview
/// </summary>
public class SimilarRole
{
    /// <summary>
    /// Role ID
    /// </summary>
    public string RoleId { get; set; } = string.Empty;

    /// <summary>
    /// Role name
    /// </summary>
    public string RoleName { get; set; } = string.Empty;

    /// <summary>
    /// Similarity score (0-100)
    /// </summary>
    public int SimilarityScore { get; set; }

    /// <summary>
    /// Number of users assigned to this role
    /// </summary>
    public int UserCount { get; set; }

    /// <summary>
    /// Common permissions between roles
    /// </summary>
    public int CommonPermissions { get; set; }

    /// <summary>
    /// Permissions only in the compared role
    /// </summary>
    public int UniquePermissions { get; set; }
}