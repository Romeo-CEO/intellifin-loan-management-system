using IntelliFin.IdentityService.Models;

namespace IntelliFin.IdentityService.Services;

public interface IKeycloakTokenClient
{
    Task<KeycloakTokenResponse> RequestClientCredentialsTokenAsync(
        string clientId,
        string clientSecret,
        IEnumerable<string>? scopes,
        CancellationToken cancellationToken = default);
}
