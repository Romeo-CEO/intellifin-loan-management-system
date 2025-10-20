using IntelliFin.IdentityService.Models;

namespace IntelliFin.IdentityService.Services;

public interface IPermissionRoleBridgeService
{
    Task<BridgeOperationResult> AssignPermissionAsync(string roleId, string permission, string tenantId, string assignedBy, CancellationToken cancellationToken = default);
    Task<BridgeOperationResult> RemovePermissionAsync(string roleId, string permission, string tenantId, string removedBy, CancellationToken cancellationToken = default);
    Task<BulkBridgeOperationResult> BulkAssignPermissionsAsync(string roleId, BulkPermissionAssignmentRequest request, string tenantId, string assignedBy, CancellationToken cancellationToken = default);
    Task<BulkBridgeOperationResult> ReplacePermissionsAsync(string roleId, ReplacePermissionsRequest request, string tenantId, string updatedBy, CancellationToken cancellationToken = default);
    Task<AvailablePermission[]> GetAvailablePermissionsAsync(string roleId, string tenantId, string? category = null, bool excludeHighRisk = false, CancellationToken cancellationToken = default);
    Task<ApplicationRole[]> GetRolesWithPermissionAsync(string permission, string tenantId, CancellationToken cancellationToken = default);
    Task<TenantPermissionUsage[]> GetPermissionUsageAnalyticsAsync(string tenantId, CancellationToken cancellationToken = default);
    Task<PermissionImpactAnalysis> AnalyzePermissionImpactAsync(string[] permissions, string tenantId, CancellationToken cancellationToken = default);
    Task<RolePermissionMatrixResponse> GetRolePermissionMatrixAsync(string tenantId, string[]? permissionFilter = null, RoleCategory? roleCategory = null, CancellationToken cancellationToken = default);
    Task<PermissionAssignment[]> GetRoleAssignmentHistoryAsync(string roleId, string tenantId, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);
    Task<PermissionChangeEntry[]> GetRecentPermissionChangesAsync(string tenantId, int maxResults = 50, DateTime? fromDate = null, CancellationToken cancellationToken = default);
    Task<PermissionValidationResult> ValidatePermissionAssignmentAsync(string roleId, string permission, string tenantId, CancellationToken cancellationToken = default);
    Task<string[]> GetRecommendedPermissionsForRoleAsync(string roleId, string tenantId, int maxRecommendations = 10, CancellationToken cancellationToken = default);
    Task<TenantPermissionHealthReport> AnalyzeTenantPermissionHealthAsync(string tenantId, CancellationToken cancellationToken = default);
}
