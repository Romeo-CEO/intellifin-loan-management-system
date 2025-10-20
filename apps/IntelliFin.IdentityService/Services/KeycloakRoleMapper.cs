using System.Security.Claims;
using System.Text.Json;

namespace IntelliFin.IdentityService.Services;

/// <summary>
/// Service to map Keycloak roles to application role claims
/// </summary>
public interface IKeycloakRoleMapper
{
    void MapRolesToClaims(ClaimsPrincipal principal);
    IEnumerable<string> ExtractRealmRoles(ClaimsPrincipal principal);
    IEnumerable<string> ExtractResourceRoles(ClaimsPrincipal principal, string clientId);
    IEnumerable<string> ExtractAllRoles(ClaimsPrincipal principal);
}

/// <summary>
/// Implementation of Keycloak role mapper
/// </summary>
public class KeycloakRoleMapper : IKeycloakRoleMapper
{
    private readonly ILogger<KeycloakRoleMapper> _logger;

    public KeycloakRoleMapper(ILogger<KeycloakRoleMapper> logger)
    {
        _logger = logger;
    }

    public void MapRolesToClaims(ClaimsPrincipal principal)
    {
        if (principal?.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
        {
            _logger.LogDebug("Principal is not authenticated, skipping role mapping");
            return;
        }

        try
        {
            // Extract all roles (realm + resource)
            var roles = ExtractAllRoles(principal);

            // Add role claims to the identity
            foreach (var role in roles)
            {
                // Avoid duplicates
                if (!identity.HasClaim(ClaimTypes.Role, role))
                {
                    identity.AddClaim(new Claim(ClaimTypes.Role, role));
                    _logger.LogDebug("Mapped Keycloak role to claim: {Role}", role);
                }
            }

            _logger.LogInformation("Successfully mapped {Count} Keycloak roles to claims", roles.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error mapping Keycloak roles to claims");
        }
    }

    public IEnumerable<string> ExtractRealmRoles(ClaimsPrincipal principal)
    {
        var roles = new List<string>();

        try
        {
            // Find the realm_access claim
            var realmAccessClaim = principal.FindFirst("realm_access")?.Value;

            if (string.IsNullOrEmpty(realmAccessClaim))
            {
                _logger.LogDebug("No realm_access claim found");
                return roles;
            }

            // Parse JSON
            using var document = JsonDocument.Parse(realmAccessClaim);
            
            if (document.RootElement.TryGetProperty("roles", out var rolesElement) &&
                rolesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var roleElement in rolesElement.EnumerateArray())
                {
                    var role = roleElement.GetString();
                    if (!string.IsNullOrEmpty(role))
                    {
                        roles.Add(role);
                        _logger.LogDebug("Extracted realm role: {Role}", role);
                    }
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing realm_access JSON");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting realm roles");
        }

        return roles;
    }

    public IEnumerable<string> ExtractResourceRoles(ClaimsPrincipal principal, string clientId)
    {
        var roles = new List<string>();

        if (string.IsNullOrEmpty(clientId))
        {
            _logger.LogWarning("Client ID is null or empty, cannot extract resource roles");
            return roles;
        }

        try
        {
            // Find the resource_access claim
            var resourceAccessClaim = principal.FindFirst("resource_access")?.Value;

            if (string.IsNullOrEmpty(resourceAccessClaim))
            {
                _logger.LogDebug("No resource_access claim found");
                return roles;
            }

            // Parse JSON
            using var document = JsonDocument.Parse(resourceAccessClaim);
            
            // Navigate to the client's roles
            if (document.RootElement.TryGetProperty(clientId, out var clientElement) &&
                clientElement.TryGetProperty("roles", out var rolesElement) &&
                rolesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var roleElement in rolesElement.EnumerateArray())
                {
                    var role = roleElement.GetString();
                    if (!string.IsNullOrEmpty(role))
                    {
                        roles.Add(role);
                        _logger.LogDebug("Extracted resource role for {ClientId}: {Role}", clientId, role);
                    }
                }
            }
            else
            {
                _logger.LogDebug("No roles found for client {ClientId}", clientId);
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing resource_access JSON");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting resource roles for client {ClientId}", clientId);
        }

        return roles;
    }

    public IEnumerable<string> ExtractAllRoles(ClaimsPrincipal principal)
    {
        var allRoles = new HashSet<string>();

        try
        {
            // Extract realm roles
            var realmRoles = ExtractRealmRoles(principal);
            foreach (var role in realmRoles)
            {
                allRoles.Add(role);
            }

            // Extract client ID for resource roles
            var clientId = principal.FindFirst("azp")?.Value 
                          ?? principal.FindFirst("client_id")?.Value;

            if (!string.IsNullOrEmpty(clientId))
            {
                var resourceRoles = ExtractResourceRoles(principal, clientId);
                foreach (var role in resourceRoles)
                {
                    allRoles.Add(role);
                }
            }
            else
            {
                _logger.LogDebug("No client ID found (azp or client_id), skipping resource roles");
            }

            // Also check if there are any direct role claims already present
            var existingRoles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value);
            foreach (var role in existingRoles)
            {
                allRoles.Add(role);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting all roles");
        }

        return allRoles;
    }
}

/// <summary>
/// Keycloak realm access model for deserialization
/// </summary>
public class KeycloakRealmAccess
{
    public string[] Roles { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Keycloak resource access model for deserialization
/// </summary>
public class KeycloakResourceAccess
{
    public Dictionary<string, KeycloakClientRoles> Clients { get; set; } = new();
}

/// <summary>
/// Keycloak client roles model
/// </summary>
public class KeycloakClientRoles
{
    public string[] Roles { get; set; } = Array.Empty<string>();
}
