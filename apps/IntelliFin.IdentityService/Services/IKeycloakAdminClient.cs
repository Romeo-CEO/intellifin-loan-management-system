using IntelliFin.Shared.DomainModels.Entities;

namespace IntelliFin.IdentityService.Services;

public record KeycloakClientRegistrationResult(string ClientId, string Secret, string? VaultPath);

public interface IKeycloakAdminClient
{
    Task<KeycloakClientRegistrationResult?> RegisterServiceAccountAsync(
        ServiceAccount account,
        string plainSecret,
        IReadOnlyCollection<string> scopes,
        CancellationToken cancellationToken = default);
}

public sealed class NullKeycloakAdminClient : IKeycloakAdminClient
{
    public Task<KeycloakClientRegistrationResult?> RegisterServiceAccountAsync(
        ServiceAccount account,
        string plainSecret,
        IReadOnlyCollection<string> scopes,
        CancellationToken cancellationToken = default)
        => Task.FromResult<KeycloakClientRegistrationResult?>(null);
}
