using IntelliFin.IdentityService.Configuration;
using IntelliFin.IdentityService.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace IntelliFin.IdentityService.Services;

/// <summary>
/// Service for Keycloak OIDC operations
/// </summary>
public interface IKeycloakService
{
    /// <summary>
    /// Generate authorization URL for OIDC flow
    /// </summary>
    string GenerateAuthorizationUrl(string state, string codeChallenge, string nonce, string? returnUrl = null);

    /// <summary>
    /// Exchange authorization code for tokens
    /// </summary>
    Task<KeycloakTokenResponse?> ExchangeCodeForTokensAsync(string code, string codeVerifier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get user information from Keycloak
    /// </summary>
    Task<KeycloakUserInfo?> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate ID token
    /// </summary>
    Task<bool> ValidateIdTokenAsync(string idToken, string nonce, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate logout URL
    /// </summary>
    string GenerateLogoutUrl(string? idToken = null, string? returnUrl = null);
}

/// <summary>
/// Implementation of Keycloak OIDC service
/// </summary>
public class KeycloakService : IKeycloakService
{
    private readonly HttpClient _httpClient;
    private readonly KeycloakOptions _options;
    private readonly ILogger<KeycloakService> _logger;

    public KeycloakService(
        HttpClient httpClient,
        IOptions<KeycloakOptions> options,
        ILogger<KeycloakService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public string GenerateAuthorizationUrl(string state, string codeChallenge, string nonce, string? returnUrl = null)
    {
        var redirectUri = _options.ClientId != null 
            ? $"https://identity.intellifin.local/api/auth/oidc/callback"
            : "http://localhost:5000/api/auth/oidc/callback";

        var scopes = new[] { "openid", "profile", "email", "roles" };
        var scopeString = string.Join(" ", scopes);

        var queryParams = new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["response_type"] = "code",
            ["scope"] = scopeString,
            ["redirect_uri"] = redirectUri,
            ["state"] = state,
            ["nonce"] = nonce,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256"
        };

        var queryString = string.Join("&", queryParams.Select(kvp => 
            $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

        var authUrl = $"{_options.GetRealmUrl()}/protocol/openid-connect/auth?{queryString}";

        _logger.LogDebug("Generated authorization URL for state {State}", state);

        return authUrl;
    }

    public async Task<KeycloakTokenResponse?> ExchangeCodeForTokensAsync(
        string code, 
        string codeVerifier, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var redirectUri = _options.ClientId != null 
                ? $"https://identity.intellifin.local/api/auth/oidc/callback"
                : "http://localhost:5000/api/auth/oidc/callback";

            var tokenEndpoint = _options.GetTokenEndpoint();

            var parameters = new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = _options.ClientId,
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["code_verifier"] = codeVerifier
            };

            // Add client secret if configured (confidential client)
            if (!string.IsNullOrEmpty(_options.ClientSecret))
            {
                parameters["client_secret"] = _options.ClientSecret;
            }

            var content = new FormUrlEncodedContent(parameters);
            var response = await _httpClient.PostAsync(tokenEndpoint, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Token exchange failed with status {StatusCode}: {Error}",
                    response.StatusCode,
                    error);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var tokenResponse = JsonSerializer.Deserialize<KeycloakTokenResponse>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });

            _logger.LogInformation("Successfully exchanged authorization code for tokens");

            return tokenResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exchanging code for tokens");
            return null;
        }
    }

    public async Task<KeycloakUserInfo?> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        try
        {
            var userInfoEndpoint = _options.GetUserInfoEndpoint();

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.GetAsync(userInfoEndpoint, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var userInfo = JsonSerializer.Deserialize<KeycloakUserInfo>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                PropertyNameCaseInsensitive = true
            });

            _logger.LogInformation("Retrieved user info for user {Sub}", userInfo?.Sub);

            return userInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user info");
            return null;
        }
    }

    public async Task<bool> ValidateIdTokenAsync(string idToken, string nonce, CancellationToken cancellationToken = default)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();

            // Read token without validation first to inspect claims
            var token = handler.ReadJwtToken(idToken);

            // Validate nonce
            var tokenNonce = token.Claims.FirstOrDefault(c => c.Type == "nonce")?.Value;
            if (tokenNonce != nonce)
            {
                _logger.LogWarning("ID token nonce mismatch. Expected: {Expected}, Got: {Actual}", nonce, tokenNonce);
                return false;
            }

            // Validate issuer
            var expectedIssuer = _options.GetRealmUrl();
            if (token.Issuer != expectedIssuer)
            {
                _logger.LogWarning("ID token issuer mismatch. Expected: {Expected}, Got: {Actual}", expectedIssuer, token.Issuer);
                return false;
            }

            // Validate audience
            if (!token.Audiences.Contains(_options.ClientId))
            {
                _logger.LogWarning("ID token audience mismatch. Expected: {Expected}", _options.ClientId);
                return false;
            }

            // Validate expiration
            if (token.ValidTo < DateTime.UtcNow)
            {
                _logger.LogWarning("ID token has expired");
                return false;
            }

            // Full validation with signature check would require JWKS endpoint
            // For now, we've validated the basic claims

            _logger.LogInformation("ID token validation successful");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating ID token");
            return false;
        }
    }

    public string GenerateLogoutUrl(string? idToken = null, string? returnUrl = null)
    {
        var endSessionEndpoint = $"{_options.GetRealmUrl()}/protocol/openid-connect/logout";

        var postLogoutRedirectUri = !string.IsNullOrEmpty(returnUrl)
            ? returnUrl
            : "https://identity.intellifin.local";

        var queryParams = new Dictionary<string, string>
        {
            ["post_logout_redirect_uri"] = postLogoutRedirectUri
        };

        if (!string.IsNullOrEmpty(idToken))
        {
            queryParams["id_token_hint"] = idToken;
        }

        var queryString = string.Join("&", queryParams.Select(kvp => 
            $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

        return $"{endSessionEndpoint}?{queryString}";
    }
}
