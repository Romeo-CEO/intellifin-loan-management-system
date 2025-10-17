using IntelliFin.IdentityService.Models;

namespace IntelliFin.IdentityService.Services;

public interface IPermissionCheckService
{
    Task<PermissionCheckResponse> CheckPermissionAsync(PermissionCheckRequest request, CancellationToken cancellationToken = default);
}
