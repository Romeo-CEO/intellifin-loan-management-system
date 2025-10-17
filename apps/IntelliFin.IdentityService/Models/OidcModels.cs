namespace IntelliFin.IdentityService.Models;

/// <summary>
/// Request for OIDC logout
/// </summary>
public class LogoutRequest
{
    public string? IdToken { get; set; }
    public string? ReturnUrl { get; set; }
}

/// <summary>
/// Response from OIDC logout
/// </summary>
public class LogoutResponse
{
    public string LogoutUrl { get; set; } = string.Empty;
    public bool Success { get; set; } = true;
}

/// <summary>
/// Token response from Keycloak
/// </summary>
public class KeycloakTokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string IdToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
    public string TokenType { get; set; } = "Bearer";
    public string Scope { get; set; } = string.Empty;
}

/// <summary>
/// User information from Keycloak
/// </summary>
public class KeycloakUserInfo
{
    public string Sub { get; set; } = string.Empty;
    public string PreferredUsername { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool EmailVerified { get; set; }
    public string GivenName { get; set; } = string.Empty;
    public string FamilyName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, object>? Attributes { get; set; }
    public List<string>? RealmRoles { get; set; }
}

/// <summary>
/// Complete OIDC login response - backward compatible with existing TokenResponse
/// </summary>
public class OidcLoginResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string IdToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
    public string TokenType { get; set; } = "Bearer";
    public UserInfo User { get; set; } = new();
}

/// <summary>
/// User information in response - matches existing format
/// </summary>
public class UserInfo
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
    public List<string> Permissions { get; set; } = new();
    
    // Branch context
    public string? BranchId { get; set; }
    public string? BranchName { get; set; }
    public string? BranchRegion { get; set; }
    
    // Tenant context (new fields)
    public string? TenantId { get; set; }
    public string? TenantName { get; set; }
}
