using IntelliFin.IdentityService.Models;
using IntelliFin.Shared.DomainModels.Entities;

namespace IntelliFin.IdentityService.Services;

public record KeycloakClientRegistrationResult(string ClientId, string Secret, string? VaultPath);

// Null implementation covering all admin operations when Keycloak integration is disabled
public sealed class NullKeycloakAdminClient : IKeycloakAdminClient
{
    public Task<string?> CreateUserAsync(KeycloakUserRepresentation user, CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);

    public Task<KeycloakUserRepresentation?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default)
        => Task.FromResult<KeycloakUserRepresentation?>(null);

    public Task<KeycloakUserRepresentation?> GetUserByIdAsync(string userId, CancellationToken cancellationToken = default)
        => Task.FromResult<KeycloakUserRepresentation?>(null);

    public Task<bool> UpdateUserAsync(string userId, KeycloakUserRepresentation user, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<bool> DeleteUserAsync(string userId, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<bool> SetTemporaryPasswordAsync(string userId, string password, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<bool> AssignRealmRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<bool> RemoveRealmRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<KeycloakClientRegistrationResult?> RegisterServiceAccountAsync(
        ServiceAccount account,
        string plainSecret,
        IReadOnlyCollection<string> scopes,
        CancellationToken cancellationToken = default)
        => Task.FromResult<KeycloakClientRegistrationResult?>(null);

    public Task<string?> GetAdminAccessTokenAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);
}
