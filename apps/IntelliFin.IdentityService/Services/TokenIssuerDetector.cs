using IntelliFin.IdentityService.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace IntelliFin.IdentityService.Services;

/// <summary>
/// Service to detect the issuer of a JWT token (custom vs Keycloak)
/// </summary>
public interface ITokenIssuerDetector
{
    TokenIssuerType DetectIssuer(string token);
    TokenIssuerType DetectIssuerFromPrincipal(ClaimsPrincipal principal);
    bool IsKeycloakToken(string token);
    bool IsCustomJwtToken(string token);
}

/// <summary>
/// Token issuer types
/// </summary>
public enum TokenIssuerType
{
    Unknown,
    CustomJwt,
    Keycloak
}

/// <summary>
/// Implementation of token issuer detector
/// </summary>
public class TokenIssuerDetector : ITokenIssuerDetector
{
    private readonly JwtConfiguration _jwtConfig;
    private readonly KeycloakOptions _keycloakConfig;
    private readonly ILogger<TokenIssuerDetector> _logger;
    private readonly JwtSecurityTokenHandler _tokenHandler;

    public TokenIssuerDetector(
        IOptions<JwtConfiguration> jwtConfig,
        IOptions<KeycloakOptions> keycloakConfig,
        ILogger<TokenIssuerDetector> logger)
    {
        _jwtConfig = jwtConfig.Value;
        _keycloakConfig = keycloakConfig.Value;
        _logger = logger;
        _tokenHandler = new JwtSecurityTokenHandler();
    }

    public TokenIssuerType DetectIssuer(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Token is null or empty");
            return TokenIssuerType.Unknown;
        }

        try
        {
            // Remove "Bearer " prefix if present
            if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                token = token.Substring(7);
            }

            // Try to read the token without validation
            if (!_tokenHandler.CanReadToken(token))
            {
                _logger.LogWarning("Token is not a valid JWT format");
                return TokenIssuerType.Unknown;
            }

            var jwtToken = _tokenHandler.ReadJwtToken(token);

            // Check the issuer claim
            var issuer = jwtToken.Issuer;

            if (string.IsNullOrEmpty(issuer))
            {
                _logger.LogWarning("Token has no issuer claim");
                return TokenIssuerType.Unknown;
            }

            // Check if it matches Keycloak realm URL
            if (_keycloakConfig.Enabled)
            {
                var keycloakRealmUrl = _keycloakConfig.GetRealmUrl();
                if (issuer.Equals(keycloakRealmUrl, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug("Detected Keycloak token from issuer: {Issuer}", issuer);
                    return TokenIssuerType.Keycloak;
                }
            }

            // Check if it matches custom JWT issuer
            if (issuer.Equals(_jwtConfig.Issuer, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Detected custom JWT token from issuer: {Issuer}", issuer);
                return TokenIssuerType.CustomJwt;
            }

            // Additional checks for Keycloak tokens
            // Keycloak tokens typically have specific claims like 'realm_access', 'resource_access', 'azp'
            if (jwtToken.Claims.Any(c => c.Type == "realm_access") ||
                jwtToken.Claims.Any(c => c.Type == "resource_access") ||
                jwtToken.Claims.Any(c => c.Type == "azp"))
            {
                _logger.LogDebug("Detected Keycloak token from specific claims");
                return TokenIssuerType.Keycloak;
            }

            _logger.LogWarning("Unknown token issuer: {Issuer}", issuer);
            return TokenIssuerType.Unknown;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting token issuer");
            return TokenIssuerType.Unknown;
        }
    }

    public TokenIssuerType DetectIssuerFromPrincipal(ClaimsPrincipal principal)
    {
        if (principal?.Identity == null || !principal.Identity.IsAuthenticated)
        {
            return TokenIssuerType.Unknown;
        }

        // Check the issuer claim from the principal
        var issuerClaim = principal.FindFirst("iss")?.Value;
        
        if (string.IsNullOrEmpty(issuerClaim))
        {
            _logger.LogWarning("Principal has no issuer claim");
            return TokenIssuerType.Unknown;
        }

        // Check if it matches Keycloak
        if (_keycloakConfig.Enabled)
        {
            var keycloakRealmUrl = _keycloakConfig.GetRealmUrl();
            if (issuerClaim.Equals(keycloakRealmUrl, StringComparison.OrdinalIgnoreCase))
            {
                return TokenIssuerType.Keycloak;
            }
        }

        // Check if it matches custom JWT
        if (issuerClaim.Equals(_jwtConfig.Issuer, StringComparison.OrdinalIgnoreCase))
        {
            return TokenIssuerType.CustomJwt;
        }

        // Check for Keycloak-specific claims
        if (principal.HasClaim(c => c.Type == "realm_access") ||
            principal.HasClaim(c => c.Type == "resource_access"))
        {
            return TokenIssuerType.Keycloak;
        }

        return TokenIssuerType.Unknown;
    }

    public bool IsKeycloakToken(string token)
    {
        return DetectIssuer(token) == TokenIssuerType.Keycloak;
    }

    public bool IsCustomJwtToken(string token)
    {
        return DetectIssuer(token) == TokenIssuerType.CustomJwt;
    }
}
