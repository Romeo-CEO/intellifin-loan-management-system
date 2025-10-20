namespace IntelliFin.IdentityService.Configuration;

/// <summary>
/// Configuration options for Keycloak integration
/// </summary>
public class KeycloakOptions
{
    public const string SectionName = "Keycloak";

    /// <summary>
    /// Base URL of the Keycloak server (e.g., https://keycloak.intellifin.local:8443)
    /// </summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary>
    /// Keycloak realm name
    /// </summary>
    public string Realm { get; set; } = string.Empty;

    /// <summary>
    /// Client ID for the IdentityService in Keycloak
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Client secret for confidential client flows (stored in Vault)
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Admin username for Keycloak management operations
    /// </summary>
    public string AdminUsername { get; set; } = string.Empty;

    /// <summary>
    /// Admin password for Keycloak management operations (stored in Vault)
    /// </summary>
    public string? AdminPassword { get; set; }

    /// <summary>
    /// Enable Keycloak OIDC authentication
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Enable dual token validation mode (both JWT and Keycloak)
    /// </summary>
    public bool DualModeEnabled { get; set; } = false;

    /// <summary>
    /// Require HTTPS for metadata and token endpoints
    /// </summary>
    public bool RequireHttpsMetadata { get; set; } = true;

    /// <summary>
    /// Token validation parameters
    /// </summary>
    public KeycloakTokenValidationOptions TokenValidation { get; set; } = new();

    /// <summary>
    /// Scopes required for user authentication flows
    /// </summary>
    public string[] RequiredScopes { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Connection settings for Keycloak HTTP client
    /// </summary>
    public KeycloakConnectionOptions Connection { get; set; } = new();

    /// <summary>
    /// Vault paths for sensitive Keycloak secrets
    /// </summary>
    public KeycloakVaultOptions Vault { get; set; } = new();

    /// <summary>
    /// Compute full authority URL with realm
    /// </summary>
    public string GetRealmUrl() => $"{Authority}/realms/{Realm}";

    /// <summary>
    /// Get OIDC discovery endpoint
    /// </summary>
    public string GetDiscoveryEndpoint() => $"{GetRealmUrl()}/.well-known/openid-configuration";

    /// <summary>
    /// Get token endpoint URL
    /// </summary>
    public string GetTokenEndpoint() => $"{GetRealmUrl()}/protocol/openid-connect/token";

    /// <summary>
    /// Get token introspection endpoint URL
    /// </summary>
    public string GetIntrospectionEndpoint() => $"{GetRealmUrl()}/protocol/openid-connect/token/introspect";

    /// <summary>
    /// Get user info endpoint URL
    /// </summary>
    public string GetUserInfoEndpoint() => $"{GetRealmUrl()}/protocol/openid-connect/userinfo";

    /// <summary>
    /// Get admin API base URL
    /// </summary>
    public string GetAdminApiUrl() => $"{Authority}/admin/realms/{Realm}";
}

/// <summary>
/// Token validation configuration for Keycloak tokens
/// </summary>
public class KeycloakTokenValidationOptions
{
    /// <summary>
    /// Validate token issuer
    /// </summary>
    public bool ValidateIssuer { get; set; } = true;

    /// <summary>
    /// Validate token audience
    /// </summary>
    public bool ValidateAudience { get; set; } = true;

    /// <summary>
    /// Validate token lifetime
    /// </summary>
    public bool ValidateLifetime { get; set; } = true;

    /// <summary>
    /// Validate token signature with Keycloak public key
    /// </summary>
    public bool ValidateIssuerSigningKey { get; set; } = true;

    /// <summary>
    /// Require expiration time in token
    /// </summary>
    public bool RequireExpirationTime { get; set; } = true;

    /// <summary>
    /// Clock skew tolerance in minutes
    /// </summary>
    public int ClockSkewMinutes { get; set; } = 5;

    /// <summary>
    /// Cache duration for OIDC configuration in minutes
    /// </summary>
    public int MetadataCacheDurationMinutes { get; set; } = 60;

    /// <summary>
    /// Validate token signature keys before caching
    /// </summary>
    public bool ValidateSigningKeys { get; set; } = true;
}

/// <summary>
/// HTTP connection settings for Keycloak client
/// </summary>
public class KeycloakConnectionOptions
{
    /// <summary>
    /// HTTP request timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum retry attempts for failed requests
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Base delay for exponential backoff in milliseconds
    /// </summary>
    public int RetryDelayMs { get; set; } = 1000;

    /// <summary>
    /// Enable HTTP connection pooling
    /// </summary>
    public bool UseConnectionPooling { get; set; } = true;

    /// <summary>
    /// Maximum connections per server
    /// </summary>
    public int MaxConnectionsPerServer { get; set; } = 10;
}

/// <summary>
/// Vault paths for Keycloak secrets
/// </summary>
public class KeycloakVaultOptions
{
    /// <summary>
    /// Vault path for Keycloak admin credentials
    /// </summary>
    public string AdminCredentialsPath { get; set; } = "/vault/secrets/keycloak-admin-credentials.json";

    /// <summary>
    /// Vault path for Keycloak client secret
    /// </summary>
    public string ClientSecretPath { get; set; } = "/vault/secrets/keycloak-client-secret.json";

    /// <summary>
    /// Keycloak role in Vault for dynamic secret generation
    /// </summary>
    public string VaultRole { get; set; } = "keycloak-identity-service";
}
