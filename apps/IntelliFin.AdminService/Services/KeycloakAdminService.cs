using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using IntelliFin.AdminService.Contracts.Requests;
using IntelliFin.AdminService.Contracts.Responses;
using IntelliFin.AdminService.ExceptionHandling;
using IntelliFin.AdminService.Models.Keycloak;
using IntelliFin.AdminService.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntelliFin.AdminService.Services;

public sealed class KeycloakAdminService : IKeycloakAdminService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly IKeycloakTokenService _tokenService;
    private readonly IOptionsMonitor<KeycloakOptions> _optionsMonitor;
    private readonly ILogger<KeycloakAdminService> _logger;

    public KeycloakAdminService(
        HttpClient httpClient,
        IKeycloakTokenService tokenService,
        IOptionsMonitor<KeycloakOptions> optionsMonitor,
        ILogger<KeycloakAdminService> logger)
    {
        _httpClient = httpClient;
        _tokenService = tokenService;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public async Task<PagedResult<UserResponse>> GetUsersAsync(int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        var safePage = Math.Max(pageNumber, 1);
        var safeSize = Math.Clamp(pageSize, 1, 200);
        var first = (safePage - 1) * safeSize;

        var listRequest = await CreateRequestAsync(HttpMethod.Get, $"admin/realms/{Realm}/users?first={first}&max={safeSize}", cancellationToken);
        var listResponse = await SendAsync(listRequest, cancellationToken);
        var users = await listResponse.Content.ReadFromJsonAsync<List<KeycloakUserRepresentation>>(SerializerOptions, cancellationToken)
                    ?? new List<KeycloakUserRepresentation>();

        var countRequest = await CreateRequestAsync(HttpMethod.Get, $"admin/realms/{Realm}/users/count", cancellationToken);
        var countResponse = await SendAsync(countRequest, cancellationToken);
        var total = await ParseCountAsync(countResponse, cancellationToken);

        var mapped = users
            .Where(u => !string.IsNullOrWhiteSpace(u.Id))
            .Select(MapUser)
            .ToList();

        return new PagedResult<UserResponse>(mapped, safePage, safeSize, total);
    }

    public async Task<UserResponse?> GetUserAsync(string id, CancellationToken cancellationToken)
    {
        var user = await GetUserRepresentationAsync(id, cancellationToken);
        return user is null || string.IsNullOrWhiteSpace(user.Id)
            ? null
            : MapUser(user);
    }

    public async Task<UserResponse> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var representation = new KeycloakUserRepresentation
        {
            Username = request.Username,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Enabled = request.Enabled,
            EmailVerified = request.EmailVerified,
            Attributes = ConvertAttributes(request.Attributes)
        };

        var createRequest = await CreateRequestAsync(HttpMethod.Post, $"admin/realms/{Realm}/users", cancellationToken, representation);
        var response = await SendAsync(createRequest, cancellationToken);

        var newId = ExtractIdFromLocation(response.Headers.Location);
        var createdRepresentation = newId is not null
            ? await GetUserRepresentationAsync(newId, cancellationToken)
            : await FindUserByUsernameAsync(request.Username, cancellationToken);

        if (createdRepresentation is null)
        {
            throw new KeycloakAdminException(HttpStatusCode.InternalServerError, "Keycloak did not return created user", null, null);
        }

        return MapUser(createdRepresentation);
    }

    public async Task<UserResponse> UpdateUserAsync(string id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var existing = await GetUserRepresentationAsync(id, cancellationToken);
        if (existing is null || string.IsNullOrWhiteSpace(existing.Id))
        {
            throw new KeycloakAdminException(HttpStatusCode.NotFound, $"User '{id}' was not found", "not_found", null);
        }

        var updatedAttributes = MergeAttributes(existing.Attributes, request.Attributes);
        var payload = new KeycloakUserRepresentation
        {
            Id = existing.Id,
            Username = existing.Username,
            Email = request.Email ?? existing.Email,
            FirstName = request.FirstName ?? existing.FirstName,
            LastName = request.LastName ?? existing.LastName,
            Enabled = request.Enabled ?? existing.Enabled,
            EmailVerified = request.EmailVerified ?? existing.EmailVerified,
            Attributes = updatedAttributes
        };

        var updateRequest = await CreateRequestAsync(HttpMethod.Put, $"admin/realms/{Realm}/users/{existing.Id}", cancellationToken, payload);
        await SendAsync(updateRequest, cancellationToken);

        var updated = await GetUserRepresentationAsync(existing.Id!, cancellationToken);
        return updated is null
            ? throw new KeycloakAdminException(HttpStatusCode.InternalServerError, "Updated user representation was not returned", null, null)
            : MapUser(updated);
    }

    public async Task DeleteUserAsync(string id, CancellationToken cancellationToken)
    {
        var request = await CreateRequestAsync(HttpMethod.Delete, $"admin/realms/{Realm}/users/{id}", cancellationToken);
        await SendAsync(request, cancellationToken);
    }

    public async Task ResetUserPasswordAsync(string id, ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var credential = new KeycloakCredentialRepresentation
        {
            Value = request.TemporaryPassword,
            Temporary = request.Temporary
        };

        var resetRequest = await CreateRequestAsync(HttpMethod.Put, $"admin/realms/{Realm}/users/{id}/reset-password", cancellationToken, credential);
        await SendAsync(resetRequest, cancellationToken);
    }

    public async Task<IReadOnlyCollection<RoleResponse>> GetRolesAsync(CancellationToken cancellationToken)
    {
        var request = await CreateRequestAsync(HttpMethod.Get, $"admin/realms/{Realm}/roles", cancellationToken);
        var response = await SendAsync(request, cancellationToken);
        var roles = await response.Content.ReadFromJsonAsync<List<KeycloakRoleRepresentation>>(SerializerOptions, cancellationToken)
                    ?? new List<KeycloakRoleRepresentation>();

        return roles
            .Where(r => !string.IsNullOrWhiteSpace(r.Name) && !string.IsNullOrWhiteSpace(r.Id))
            .Select(MapRole)
            .ToList();
    }

    public async Task<RoleResponse?> GetRoleAsync(string name, CancellationToken cancellationToken)
    {
        var role = await GetRoleRepresentationAsync(name, cancellationToken);
        return role is null || string.IsNullOrWhiteSpace(role.Id)
            ? null
            : MapRole(role);
    }

    public async Task<RoleResponse> CreateRoleAsync(CreateRoleRequest request, CancellationToken cancellationToken)
    {
        var payload = new KeycloakRoleRepresentation
        {
            Name = request.Name,
            Description = request.Description
        };

        var createRequest = await CreateRequestAsync(HttpMethod.Post, $"admin/realms/{Realm}/roles", cancellationToken, payload);
        await SendAsync(createRequest, cancellationToken);

        var created = await GetRoleAsync(request.Name, cancellationToken);
        return created ?? throw new KeycloakAdminException(HttpStatusCode.InternalServerError, "Keycloak did not return created role", null, null);
    }

    public async Task<RoleResponse> UpdateRoleAsync(string name, UpdateRoleRequest request, CancellationToken cancellationToken)
    {
        var existing = await GetRoleRepresentationAsync(name, cancellationToken);
        if (existing is null || string.IsNullOrWhiteSpace(existing.Id))
        {
            throw new KeycloakAdminException(HttpStatusCode.NotFound, $"Role '{name}' was not found", "not_found", null);
        }

        var payload = new KeycloakRoleRepresentation
        {
            Id = existing.Id,
            Name = request.Name ?? existing.Name,
            Description = request.Description ?? existing.Description,
            Composite = existing.Composite,
            ClientRole = existing.ClientRole,
            ContainerId = existing.ContainerId
        };

        var updateRequest = await CreateRequestAsync(HttpMethod.Put, $"admin/realms/{Realm}/roles/{existing.Name}", cancellationToken, payload);
        await SendAsync(updateRequest, cancellationToken);

        var updated = await GetRoleRepresentationAsync(payload.Name ?? existing.Name!, cancellationToken);
        return updated is null
            ? throw new KeycloakAdminException(HttpStatusCode.InternalServerError, "Updated role representation was not returned", null, null)
            : MapRole(updated);
    }

    public async Task DeleteRoleAsync(string name, CancellationToken cancellationToken)
    {
        var request = await CreateRequestAsync(HttpMethod.Delete, $"admin/realms/{Realm}/roles/{name}", cancellationToken);
        await SendAsync(request, cancellationToken);
    }

    public async Task<IReadOnlyCollection<RoleResponse>> GetUserRolesAsync(string id, CancellationToken cancellationToken)
    {
        var request = await CreateRequestAsync(HttpMethod.Get, $"admin/realms/{Realm}/users/{id}/role-mappings/realm", cancellationToken);
        var response = await SendAsync(request, cancellationToken);
        var roles = await response.Content.ReadFromJsonAsync<List<KeycloakRoleRepresentation>>(SerializerOptions, cancellationToken)
                    ?? new List<KeycloakRoleRepresentation>();

        return roles
            .Where(r => !string.IsNullOrWhiteSpace(r.Name) && !string.IsNullOrWhiteSpace(r.Id))
            .Select(MapRole)
            .ToList();
    }

    public async Task AssignRolesAsync(string id, AssignRolesRequest request, CancellationToken cancellationToken)
    {
        var roles = new List<KeycloakRoleRepresentation>();
        foreach (var roleName in request.Roles)
        {
            var role = await GetRoleRepresentationAsync(roleName, cancellationToken);
            if (role is null || string.IsNullOrWhiteSpace(role.Id))
            {
                throw new KeycloakAdminException(HttpStatusCode.NotFound, $"Role '{roleName}' was not found", "not_found", null);
            }
            roles.Add(role);
        }

        var assignRequest = await CreateRequestAsync(HttpMethod.Post, $"admin/realms/{Realm}/users/{id}/role-mappings/realm", cancellationToken, roles);
        await SendAsync(assignRequest, cancellationToken);
    }

    public async Task RemoveRoleAsync(string id, string roleName, CancellationToken cancellationToken)
    {
        var role = await GetRoleRepresentationAsync(roleName, cancellationToken);
        if (role is null || string.IsNullOrWhiteSpace(role.Id))
        {
            throw new KeycloakAdminException(HttpStatusCode.NotFound, $"Role '{roleName}' was not found", "not_found", null);
        }

        var removeRequest = await CreateRequestAsync(HttpMethod.Delete, $"admin/realms/{Realm}/users/{id}/role-mappings/realm", cancellationToken, new[] { role });
        await SendAsync(removeRequest, cancellationToken);
    }

    public async Task SetUserAttributeAsync(string id, string attributeName, string value, CancellationToken cancellationToken)
    {
        var existing = await GetUserRepresentationAsync(id, cancellationToken);
        if (existing is null || string.IsNullOrWhiteSpace(existing.Id))
        {
            throw new KeycloakAdminException(HttpStatusCode.NotFound, $"User '{id}' was not found", "not_found", null);
        }

        var updates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [attributeName] = value
        };

        var attributes = MergeAttributes(existing.Attributes, updates)
            ?? new Dictionary<string, IList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                [attributeName] = new List<string> { value }
            };

        var payload = new KeycloakUserRepresentation
        {
            Id = existing.Id,
            Username = existing.Username,
            FirstName = existing.FirstName,
            LastName = existing.LastName,
            Email = existing.Email,
            Enabled = existing.Enabled,
            EmailVerified = existing.EmailVerified,
            Attributes = attributes
        };

        var request = await CreateRequestAsync(HttpMethod.Put, $"admin/realms/{Realm}/users/{existing.Id}", cancellationToken, payload);
        await SendAsync(request, cancellationToken);
    }

    public async Task RemoveUserAttributeAsync(string id, string attributeName, CancellationToken cancellationToken)
    {
        var existing = await GetUserRepresentationAsync(id, cancellationToken);
        if (existing is null || string.IsNullOrWhiteSpace(existing.Id) || existing.Attributes is null)
        {
            return;
        }

        var attributes = existing.Attributes
            .Where(kvp => !string.Equals(kvp.Key, attributeName, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(kvp => kvp.Key, kvp => (IList<string>)new List<string>(kvp.Value), StringComparer.OrdinalIgnoreCase);

        var payload = new KeycloakUserRepresentation
        {
            Id = existing.Id,
            Username = existing.Username,
            FirstName = existing.FirstName,
            LastName = existing.LastName,
            Email = existing.Email,
            Enabled = existing.Enabled,
            EmailVerified = existing.EmailVerified,
            Attributes = attributes
        };

        var request = await CreateRequestAsync(HttpMethod.Put, $"admin/realms/{Realm}/users/{existing.Id}", cancellationToken, payload);
        await SendAsync(request, cancellationToken);
    }

    public async Task InvalidateUserSessionsAsync(string id, CancellationToken cancellationToken)
    {
        var request = await CreateRequestAsync(HttpMethod.Post, $"admin/realms/{Realm}/users/{id}/logout", cancellationToken);
        await SendAsync(request, cancellationToken);
    }

    private string Realm => _optionsMonitor.CurrentValue.Realm;

    private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string path, CancellationToken cancellationToken, object? payload = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var token = await _tokenService.GetAccessTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload, options: SerializerOptions);
        }

        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            await response.Content.ReadAsStringAsync(cancellationToken); // drain
            throw new KeycloakAdminException(HttpStatusCode.NotFound, "Resource not found", "not_found", null);
        }

        await ThrowForErrorAsync(response, cancellationToken);
        return response;
    }

    private async Task<KeycloakUserRepresentation?> GetUserRepresentationAsync(string id, CancellationToken cancellationToken)
    {
        var request = await CreateRequestAsync(HttpMethod.Get, $"admin/realms/{Realm}/users/{id}", cancellationToken);
        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            await response.Content.ReadAsStringAsync(cancellationToken);
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            await ThrowForErrorAsync(response, cancellationToken);
        }

        return await response.Content.ReadFromJsonAsync<KeycloakUserRepresentation>(SerializerOptions, cancellationToken);
    }

    private async Task<KeycloakUserRepresentation?> FindUserByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        var request = await CreateRequestAsync(HttpMethod.Get, $"admin/realms/{Realm}/users?username={Uri.EscapeDataString(username)}&exact=true", cancellationToken);
        var response = await SendAsync(request, cancellationToken);
        var users = await response.Content.ReadFromJsonAsync<List<KeycloakUserRepresentation>>(SerializerOptions, cancellationToken)
                    ?? new List<KeycloakUserRepresentation>();

        return users.FirstOrDefault(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<KeycloakRoleRepresentation?> GetRoleRepresentationAsync(string name, CancellationToken cancellationToken)
    {
        var request = await CreateRequestAsync(HttpMethod.Get, $"admin/realms/{Realm}/roles/{name}", cancellationToken);
        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            await response.Content.ReadAsStringAsync(cancellationToken);
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            await ThrowForErrorAsync(response, cancellationToken);
        }

        return await response.Content.ReadFromJsonAsync<KeycloakRoleRepresentation>(SerializerOptions, cancellationToken);
    }

    private static async Task<int> ParseCountAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var document = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: cancellationToken);
            if (document is not null && document.RootElement.TryGetProperty("count", out var countProperty) && countProperty.TryGetInt32(out var count))
            {
                return count;
            }
        }
        catch (JsonException)
        {
            // fall back below
        }

        var fallback = await response.Content.ReadAsStringAsync(cancellationToken);
        return int.TryParse(fallback, out var parsed) ? parsed : 0;
    }

    private static string? ExtractIdFromLocation(Uri? location)
    {
        if (location is null)
        {
            return null;
        }

        var segments = location.Segments;
        return segments.Length == 0 ? null : Uri.UnescapeDataString(segments[^1].TrimEnd('/'));
    }

    private static IDictionary<string, IList<string>>? ConvertAttributes(IDictionary<string, string>? attributes)
    {
        if (attributes is null)
        {
            return null;
        }

        return attributes.ToDictionary(
            pair => pair.Key,
            pair => (IList<string>)new List<string> { pair.Value },
            StringComparer.OrdinalIgnoreCase);
    }

    private static IDictionary<string, IList<string>>? MergeAttributes(IDictionary<string, IList<string>>? existing, IDictionary<string, string>? updates)
    {
        if (existing is null && updates is null)
        {
            return existing;
        }

        var merged = existing is null
            ? new Dictionary<string, IList<string>>(StringComparer.OrdinalIgnoreCase)
            : existing.ToDictionary(kvp => kvp.Key, kvp => (IList<string>)new List<string>(kvp.Value), StringComparer.OrdinalIgnoreCase);

        if (updates is not null)
        {
            foreach (var pair in updates)
            {
                merged[pair.Key] = new List<string> { pair.Value };
            }
        }

        return merged;
    }

    private static UserResponse MapUser(KeycloakUserRepresentation representation)
    {
        var attributes = representation.Attributes?
            .ToDictionary(
                kvp => kvp.Key,
                kvp => (IReadOnlyList<string>)kvp.Value.ToList(),
                StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

        return new UserResponse(
            representation.Id ?? string.Empty,
            representation.Username ?? string.Empty,
            representation.Email,
            representation.FirstName,
            representation.LastName,
            representation.Enabled ?? false,
            representation.EmailVerified ?? false,
            attributes);
    }

    private static RoleResponse MapRole(KeycloakRoleRepresentation representation)
    {
        return new RoleResponse(
            representation.Id ?? string.Empty,
            representation.Name ?? string.Empty,
            representation.Description,
            representation.Composite ?? false,
            representation.ClientRole ?? false);
    }

    private async Task ThrowForErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string? error = null;
        string? description = null;
        string? body = null;
        try
        {
            body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(body))
            {
                using var document = JsonDocument.Parse(body);
                var root = document.RootElement;
                if (root.TryGetProperty("error", out var errorProperty))
                {
                    error = errorProperty.GetString();
                }

                if (root.TryGetProperty("error_description", out var descriptionProperty))
                {
                    description = descriptionProperty.GetString();
                }
                else if (root.TryGetProperty("errorMessage", out var messageProperty))
                {
                    description = messageProperty.GetString();
                }
            }
        }
        catch (JsonException jsonException)
        {
            _logger.LogDebug(jsonException, "Failed to parse Keycloak error response body: {Body}", body);
        }

        var message = !string.IsNullOrWhiteSpace(description)
            ? description!
            : $"Keycloak admin API returned status {(int)response.StatusCode}";

        throw new KeycloakAdminException(response.StatusCode, message, error, description);
    }
}
