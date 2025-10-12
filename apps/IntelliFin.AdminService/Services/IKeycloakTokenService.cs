namespace IntelliFin.AdminService.Services;

public interface IKeycloakTokenService
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);
}
