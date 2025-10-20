using IntelliFin.IdentityService.Models;

namespace IntelliFin.IdentityService.Services;

public interface IServiceTokenService
{
    Task<ServiceTokenResponse> GenerateTokenAsync(ClientCredentialsRequest request, CancellationToken cancellationToken = default);
}
