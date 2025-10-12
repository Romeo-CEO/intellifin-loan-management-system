using System.Net.Http.Headers;
using System.Net.Http.Json;
using IntelliFin.AdminService.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntelliFin.AdminService.Services;

public sealed class KeycloakTokenService : IKeycloakTokenService
{
    private const string CacheKey = "keycloak-admin-token";

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly IOptionsMonitor<KeycloakOptions> _optionsMonitor;
    private readonly ILogger<KeycloakTokenService> _logger;

    public KeycloakTokenService(
        HttpClient httpClient,
        IMemoryCache cache,
        IOptionsMonitor<KeycloakOptions> optionsMonitor,
        ILogger<KeycloakTokenService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue<string>(CacheKey, out var cachedToken))
        {
            return cachedToken;
        }

        var options = _optionsMonitor.CurrentValue;
        var realm = options.Realm ?? string.Empty;
        var payload = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = options.ClientId
        };

        if (!string.IsNullOrWhiteSpace(options.ClientSecret))
        {
            payload["client_secret"] = options.ClientSecret;
        }

        var content = new FormUrlEncodedContent(payload);

        var request = new HttpRequestMessage(HttpMethod.Post, $"realms/{realm}/protocol/openid-connect/token")
        {
            Content = content
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "Failed to acquire Keycloak admin token. StatusCode={Status} Body={Body}",
                response.StatusCode,
                errorBody);
            response.EnsureSuccessStatusCode();
        }

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: cancellationToken)
                    ?? throw new InvalidOperationException("Unable to deserialize Keycloak token response");

        var lifetime = TimeSpan.FromSeconds(Math.Max(token.ExpiresIn - 30, 30));
        _cache.Set(CacheKey, token.AccessToken, lifetime);
        return token.AccessToken;
    }

    private sealed record TokenResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("access_token")] string AccessToken,
        [property: System.Text.Json.Serialization.JsonPropertyName("expires_in")] int ExpiresIn,
        [property: System.Text.Json.Serialization.JsonPropertyName("token_type")] string TokenType);
}
