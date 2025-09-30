using IntelliFin.IdentityService.Models;

namespace IntelliFin.IdentityService.Services;

/// <summary>
/// Service for managing the platform permission catalog
/// </summary>
public interface IPermissionCatalogService
{
    /// <summary>
    /// Get all system permissions with optional filtering
    /// </summary>
    Task<SystemPermission[]> GetAllPermissionsAsync(
        string? category = null, 
        PermissionRiskLevel? riskLevel = null,
        bool includeDeprecated = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get permissions available to a specific tenant based on subscription tier
    /// </summary>
    Task<SystemPermission[]> GetTenantAvailablePermissionsAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get permission by ID with metadata
    /// </summary>
    Task<SystemPermission?> GetPermissionByIdAsync(
        string permissionId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get permission categories with statistics
    /// </summary>
    Task<PermissionCategory[]> GetPermissionCategoriesAsync(
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Search permissions by name, category, or description
    /// </summary>
    Task<SystemPermission[]> SearchPermissionsAsync(
        string query, 
        string? tenantId = null,
        int maxResults = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new system permission (platform admin only)
    /// </summary>
    Task<SystemPermission> CreatePermissionAsync(
        SystemPermission permission,
        string createdBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update permission metadata (platform admin only)
    /// </summary>
    Task<SystemPermission> UpdatePermissionAsync(
        string permissionId,
        SystemPermission permission,
        string updatedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deprecate a permission (soft delete)
    /// </summary>
    Task<bool> DeprecatePermissionAsync(
        string permissionId,
        string deprecatedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get permission usage analytics across tenants
    /// </summary>
    Task<PermissionUsageStats[]> GetPermissionUsageStatsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate if permission is available to tenant
    /// </summary>
    Task<bool> IsPermissionAvailableToTenantAsync(
        string permissionId, 
        string tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get recommended permissions for a role pattern
    /// </summary>
    Task<string[]> GetRecommendedPermissionsAsync(
        string[] currentPermissions,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate permission deployment impact
    /// </summary>
    Task<PermissionImpactAnalysis> AnalyzePermissionImpactAsync(
        string[] permissionIds,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Analysis of permission deployment impact
/// </summary>
public class PermissionImpactAnalysis
{
    public int AffectedTenants { get; set; }
    public int AffectedRoles { get; set; }
    public int AffectedUsers { get; set; }
    public string[] PotentialIssues { get; set; } = Array.Empty<string>();
    public string[] Recommendations { get; set; } = Array.Empty<string>();
}