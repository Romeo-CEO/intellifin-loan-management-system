using IntelliFin.IdentityService.Models;

namespace IntelliFin.IdentityService.Services;

public interface IServiceAccountService
{
    Task<ServiceAccountDto> CreateServiceAccountAsync(ServiceAccountCreateRequest request, CancellationToken ct = default);
    Task<ServiceCredentialDto> RotateSecretAsync(Guid serviceAccountId, CancellationToken ct = default);
    Task RevokeServiceAccountAsync(Guid serviceAccountId, CancellationToken ct = default);
}
