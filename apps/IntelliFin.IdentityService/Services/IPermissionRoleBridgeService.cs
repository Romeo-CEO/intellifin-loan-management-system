namespace IntelliFin.IdentityService.Services;

public interface IPermissionRoleBridgeService
{
    Task<PermissionValidationResult[]> AnalyzeTenantPermissionHealthAsync(string tenantId, CancellationToken cancellationToken = default);
    Task<BridgeOperationResult> AnalyzePermissionAssignmentAsync(string roleId, string permission, string tenantId, CancellationToken cancellationToken = default);
}
