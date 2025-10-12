using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using IntelliFin.UserMigration.Models.Keycloak;
using IntelliFin.UserMigration.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntelliFin.UserMigration.Services;

public sealed class KeycloakAdminClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly KeycloakTokenService _tokenService;
    private readonly KeycloakOptions _options;
    private readonly ILogger<KeycloakAdminClient> _logger;

    public KeycloakAdminClient(
        IHttpClientFactory httpClientFactory,
        KeycloakTokenService tokenService,
        IOptions<KeycloakOptions> options,
        ILogger<KeycloakAdminClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _tokenService = tokenService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<KeycloakUserRepresentation?> GetUserByIdAsync(string keycloakUserId, CancellationToken cancellationToken)
    {
        var client = await CreateAdminClientAsync(cancellationToken).ConfigureAwait(false);
        var response = await client.GetAsync($"admin/realms/{_options.Realm}/users/{keycloakUserId}", cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<KeycloakUserRepresentation>(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> GetRealmUsersCountAsync(CancellationToken cancellationToken)
    {
        var client = await CreateAdminClientAsync(cancellationToken).ConfigureAwait(false);
        var response = await client.GetAsync($"admin/realms/{_options.Realm}/users/count", cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (int.TryParse(json, out var count))
        {
            return count;
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("count").GetInt32();
    }

    public async Task<IReadOnlyList<KeycloakUserRepresentation>> GetRealmUsersAsync(int first, int max, CancellationToken cancellationToken)
    {
        var client = await CreateAdminClientAsync(cancellationToken).ConfigureAwait(false);
        var response = await client.GetAsync($"admin/realms/{_options.Realm}/users?first={first}&max={max}", cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var users = await response.Content.ReadFromJsonAsync<List<KeycloakUserRepresentation>>(cancellationToken: cancellationToken).ConfigureAwait(false);
        return users ?? new List<KeycloakUserRepresentation>();
    }

    public async Task<string> CreateUserAsync(KeycloakUserRepresentation user, CancellationToken cancellationToken)
    {
        var client = await CreateAdminClientAsync(cancellationToken).ConfigureAwait(false);
        var response = await client.PostAsJsonAsync($"admin/realms/{_options.Realm}/users", user, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            _logger.LogWarning("User {Username} already exists in Keycloak.", user.Username);
            var existing = await FindUserByUsernameAsync(user.Username!, cancellationToken).ConfigureAwait(false);
            if (existing is null)
            {
                throw new InvalidOperationException($"Keycloak returned conflict for user '{user.Username}' but the user could not be retrieved.");
            }

            return existing.Id ?? throw new InvalidOperationException("Existing user did not contain an id.");
        }

        response.EnsureSuccessStatusCode();
        if (response.Headers.Location is { } location && !string.IsNullOrEmpty(location.AbsolutePath))
        {
            return location.Segments.Last();
        }

        var created = await FindUserByUsernameAsync(user.Username ?? user.Email ?? throw new InvalidOperationException("Username or email must be provided"), cancellationToken).ConfigureAwait(false);
        if (created?.Id is null)
        {
            throw new InvalidOperationException("Unable to resolve created Keycloak user identifier.");
        }

        return created.Id;
    }

    public async Task DeleteUserAsync(string keycloakUserId, CancellationToken cancellationToken)
    {
        var client = await CreateAdminClientAsync(cancellationToken).ConfigureAwait(false);
        var response = await client.DeleteAsync($"admin/realms/{_options.Realm}/users/{keycloakUserId}", cancellationToken).ConfigureAwait(false);
        if (response.StatusCode is System.Net.HttpStatusCode.NotFound)
        {
            return;
        }

        response.EnsureSuccessStatusCode();
    }

    public async Task<KeycloakRoleRepresentation?> GetRealmRoleByNameAsync(string roleName, CancellationToken cancellationToken)
    {
        var client = await CreateAdminClientAsync(cancellationToken).ConfigureAwait(false);
        var response = await client.GetAsync($"admin/realms/{_options.Realm}/roles/{Uri.EscapeDataString(roleName)}", cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<KeycloakRoleRepresentation>(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<KeycloakRoleRepresentation> CreateRealmRoleAsync(KeycloakRoleRepresentation role, CancellationToken cancellationToken)
    {
        var client = await CreateAdminClientAsync(cancellationToken).ConfigureAwait(false);
        var response = await client.PostAsJsonAsync($"admin/realms/{_options.Realm}/roles", role, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            _logger.LogWarning("Role {Role} already exists in Keycloak.", role.Name);
            var existing = await GetRealmRoleByNameAsync(role.Name, cancellationToken).ConfigureAwait(false);
            if (existing is null)
            {
                throw new InvalidOperationException($"Role '{role.Name}' exists but could not be retrieved.");
            }

            return existing;
        }

        response.EnsureSuccessStatusCode();
        return await GetRealmRoleByNameAsync(role.Name, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Unable to retrieve created realm role.");
    }

    public async Task<IReadOnlyList<KeycloakRoleRepresentation>> GetRealmRolesAsync(CancellationToken cancellationToken)
    {
        var client = await CreateAdminClientAsync(cancellationToken).ConfigureAwait(false);
        var response = await client.GetAsync($"admin/realms/{_options.Realm}/roles", cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<KeycloakRoleRepresentation>>(cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? new List<KeycloakRoleRepresentation>();
    }

    public async Task DeleteRealmRoleAsync(string roleName, CancellationToken cancellationToken)
    {
        var client = await CreateAdminClientAsync(cancellationToken).ConfigureAwait(false);
        var response = await client.DeleteAsync($"admin/realms/{_options.Realm}/roles/{Uri.EscapeDataString(roleName)}", cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return;
        }

        response.EnsureSuccessStatusCode();
    }

    public async Task AssignRealmRolesToUserAsync(string keycloakUserId, IEnumerable<KeycloakRoleRepresentation> roles, CancellationToken cancellationToken)
    {
        var client = await CreateAdminClientAsync(cancellationToken).ConfigureAwait(false);
        var payload = roles.Select(role => new KeycloakRoleRepresentation
        {
            Id = role.Id,
            Name = role.Name,
            Description = role.Description,
        }).ToList();

        if (payload.Count == 0)
        {
            return;
        }

        var response = await client.PostAsJsonAsync($"admin/realms/{_options.Realm}/users/{keycloakUserId}/role-mappings/realm", payload, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<KeycloakRoleRepresentation>> GetUserRealmRoleMappingsAsync(string keycloakUserId, CancellationToken cancellationToken)
    {
        var client = await CreateAdminClientAsync(cancellationToken).ConfigureAwait(false);
        var response = await client.GetAsync($"admin/realms/{_options.Realm}/users/{keycloakUserId}/role-mappings/realm", cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<KeycloakRoleRepresentation>>(cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? new List<KeycloakRoleRepresentation>();
    }

    private async Task<KeycloakUserRepresentation?> FindUserByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        var client = await CreateAdminClientAsync(cancellationToken).ConfigureAwait(false);
        var response = await client.GetAsync($"admin/realms/{_options.Realm}/users?username={Uri.EscapeDataString(username)}&exact=true", cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var users = await response.Content.ReadFromJsonAsync<List<KeycloakUserRepresentation>>(cancellationToken: cancellationToken).ConfigureAwait(false);
        return users?.FirstOrDefault();
    }

    private async Task<HttpClient> CreateAdminClientAsync(CancellationToken cancellationToken)
    {
        var token = await _tokenService.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        var client = _httpClientFactory.CreateClient("keycloak-admin");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (_options.ApiDelayMs > 0)
        {
            await Task.Delay(_options.ApiDelayMs, cancellationToken).ConfigureAwait(false);
        }

        return client;
    }
}
