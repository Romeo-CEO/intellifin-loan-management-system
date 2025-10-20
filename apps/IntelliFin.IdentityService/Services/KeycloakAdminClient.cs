using IntelliFin.IdentityService.Configuration;
using IntelliFin.IdentityService.Models;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace IntelliFin.IdentityService.Services;

/// <summary>
/// Client for Keycloak Admin REST API operations
/// </summary>
public interface IKeycloakAdminClient
{
    // User operations
    Task<string?> CreateUserAsync(KeycloakUserRepresentation user, CancellationToken cancellationToken = default);
    Task<KeycloakUserRepresentation?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<KeycloakUserRepresentation?> GetUserByIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<bool> UpdateUserAsync(string userId, KeycloakUserRepresentation user, CancellationToken cancellationToken = default);
    Task<bool> DeleteUserAsync(string userId, CancellationToken cancellationToken = default);
    Task<bool> SetTemporaryPasswordAsync(string userId, string password, CancellationToken cancellationToken = default);
    Task<bool> AssignRealmRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default);
    Task<bool> RemoveRealmRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default);

    // Service account (client) registration
    Task<KeycloakClientRegistrationResult?> RegisterServiceAccountAsync(
        IntelliFin.Shared.DomainModels.Entities.ServiceAccount account,
        string plainSecret,
        IReadOnlyCollection<string> scopes,
        CancellationToken cancellationToken = default);

    Task<string?> GetAdminAccessTokenAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of Keycloak Admin API client
/// </summary>
public class KeycloakAdminClient : IKeycloakAdminClient
{
    private readonly HttpClient _httpClient;
    private readonly KeycloakOptions _options;
    private readonly ILogger<KeycloakAdminClient> _logger;
    private string? _adminAccessToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public KeycloakAdminClient(
        HttpClient httpClient,
        IOptions<KeycloakOptions> options,
        ILogger<KeycloakAdminClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string?> CreateUserAsync(KeycloakUserRepresentation user, CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureAdminTokenAsync(cancellationToken);

            var url = $"{_options.GetAdminApiUrl()}/users";
            var json = JsonSerializer.Serialize(user, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminAccessToken);

            var response = await _httpClient.PostAsync(url, content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                // Extract user ID from Location header
                var location = response.Headers.Location?.ToString();
                if (!string.IsNullOrEmpty(location))
                {
                    var userId = location.Substring(location.LastIndexOf('/') + 1);
                    _logger.LogInformation("Created Keycloak user: {Email}, ID: {UserId}", user.Email, userId);
                    return userId;
                }
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to create user {Email}: {StatusCode} - {Error}", 
                    user.Email, response.StatusCode, error);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user {Email} in Keycloak", user.Email);
            return null;
        }
    }

    public async Task<KeycloakUserRepresentation?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureAdminTokenAsync(cancellationToken);

            var url = $"{_options.GetAdminApiUrl()}/users?email={Uri.EscapeDataString(email)}&exact=true";
            
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminAccessToken);

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var users = JsonSerializer.Deserialize<KeycloakUserRepresentation[]>(content, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return users?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user by email {Email} from Keycloak", email);
            return null;
        }
    }

    public async Task<KeycloakUserRepresentation?> GetUserByIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureAdminTokenAsync(cancellationToken);

            var url = $"{_options.GetAdminApiUrl()}/users/{userId}";
            
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminAccessToken);

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<KeycloakUserRepresentation>(content, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user by ID {UserId} from Keycloak", userId);
            return null;
        }
    }

    public async Task<bool> UpdateUserAsync(string userId, KeycloakUserRepresentation user, CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureAdminTokenAsync(cancellationToken);

            var url = $"{_options.GetAdminApiUrl()}/users/{userId}";
            var json = JsonSerializer.Serialize(user, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminAccessToken);

            var response = await _httpClient.PutAsync(url, content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Updated Keycloak user: {UserId}", userId);
                return true;
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to update user {UserId}: {StatusCode} - {Error}", 
                    userId, response.StatusCode, error);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {UserId} in Keycloak", userId);
            return false;
        }
    }

    public async Task<bool> DeleteUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureAdminTokenAsync(cancellationToken);

            var url = $"{_options.GetAdminApiUrl()}/users/{userId}";
            
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminAccessToken);

            var response = await _httpClient.DeleteAsync(url, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Deleted Keycloak user: {UserId}", userId);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {UserId} from Keycloak", userId);
            return false;
        }
    }

    public async Task<bool> SetTemporaryPasswordAsync(string userId, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureAdminTokenAsync(cancellationToken);

            var url = $"{_options.GetAdminApiUrl()}/users/{userId}/reset-password";
            
            var credential = new
            {
                type = "password",
                value = password,
                temporary = true
            };

            var json = JsonSerializer.Serialize(credential);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminAccessToken);

            var response = await _httpClient.PutAsync(url, content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Set temporary password for user: {UserId}", userId);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting temporary password for user {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> AssignRealmRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureAdminTokenAsync(cancellationToken);

            // First, get the role by name
            var roleUrl = $"{_options.GetAdminApiUrl()}/roles/{roleName}";
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminAccessToken);
            
            var roleResponse = await _httpClient.GetAsync(roleUrl, cancellationToken);
            if (!roleResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Role {RoleName} not found in Keycloak", roleName);
                return false;
            }

            var roleContent = await roleResponse.Content.ReadAsStringAsync(cancellationToken);
            var role = JsonSerializer.Deserialize<KeycloakRoleRepresentation>(roleContent, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (role == null) return false;

            // Assign role to user
            var assignUrl = $"{_options.GetAdminApiUrl()}/users/{userId}/role-mappings/realm";
            var json = JsonSerializer.Serialize(new[] { role }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(assignUrl, content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Assigned role {RoleName} to user {UserId}", roleName, userId);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning role {RoleName} to user {UserId}", roleName, userId);
            return false;
        }
    }

    public async Task<bool> RemoveRealmRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureAdminTokenAsync(cancellationToken);

            // Get the role
            var roleUrl = $"{_options.GetAdminApiUrl()}/roles/{roleName}";
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminAccessToken);
            
            var roleResponse = await _httpClient.GetAsync(roleUrl, cancellationToken);
            if (!roleResponse.IsSuccessStatusCode) return false;

            var roleContent = await roleResponse.Content.ReadAsStringAsync(cancellationToken);
            var role = JsonSerializer.Deserialize<KeycloakRoleRepresentation>(roleContent, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (role == null) return false;

            // Remove role from user
            var removeUrl = $"{_options.GetAdminApiUrl()}/users/{userId}/role-mappings/realm";
            var json = JsonSerializer.Serialize(new[] { role }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            
            var request = new HttpRequestMessage(HttpMethod.Delete, removeUrl)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            var response = await _httpClient.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing role {RoleName} from user {UserId}", roleName, userId);
            return false;
        }
    }

    public async Task<string?> GetAdminAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var tokenUrl = $"{_options.Authority}/realms/master/protocol/openid-connect/token";

            var parameters = new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = "admin-cli",
                ["username"] = _options.AdminUsername,
                ["password"] = _options.AdminPassword ?? throw new InvalidOperationException("Admin password not configured")
            };

            var content = new FormUrlEncodedContent(parameters);
            var response = await _httpClient.PostAsync(tokenUrl, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var tokenResponse = JsonSerializer.Deserialize<KeycloakTokenResponse>(responseContent, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return tokenResponse?.AccessToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obtaining Keycloak admin access token");
            return null;
        }
    }

    // Placeholder implementation for service account registration (client credentials)
    public async Task<KeycloakClientRegistrationResult?> RegisterServiceAccountAsync(
        IntelliFin.Shared.DomainModels.Entities.ServiceAccount account,
        string plainSecret,
        IReadOnlyCollection<string> scopes,
        CancellationToken cancellationToken = default)
    {
        // Not implemented yet; return null to indicate no external provisioning performed
        _logger.LogDebug("RegisterServiceAccountAsync called for {ClientId} (scopes: {Scopes})", account.ClientId, string.Join(",", scopes));
        return await Task.FromResult<KeycloakClientRegistrationResult?>(null);
    }

    private async Task EnsureAdminTokenAsync(CancellationToken cancellationToken)
    {
        if (_adminAccessToken == null || DateTime.UtcNow >= _tokenExpiry)
        {
            _adminAccessToken = await GetAdminAccessTokenAsync(cancellationToken);
            if (_adminAccessToken == null)
            {
                throw new InvalidOperationException("Failed to obtain Keycloak admin access token");
            }

            // Set expiry to 5 minutes from now (tokens typically last longer, but we refresh early)
            _tokenExpiry = DateTime.UtcNow.AddMinutes(5);
            _logger.LogDebug("Refreshed Keycloak admin access token");
        }
    }
}

/// <summary>
/// Keycloak user representation model
/// </summary>
public class KeycloakUserRepresentation
{
    public string? Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool Enabled { get; set; } = true;
    public bool EmailVerified { get; set; } = false;
    public Dictionary<string, string[]>? Attributes { get; set; }
    public string[]? RealmRoles { get; set; }
    public long? CreatedTimestamp { get; set; }
}

/// <summary>
/// Keycloak role representation model
/// </summary>
public class KeycloakRoleRepresentation
{
    public string? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool Composite { get; set; }
    public bool ClientRole { get; set; }
    public string? ContainerId { get; set; }
}

/// <summary>
/// Keycloak token response model
/// </summary>
// Token response model moved to Models/OidcModels.cs to avoid duplicate definitions

