using IntelliFin.IdentityService.Configuration;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text.Json;

namespace IntelliFin.IdentityService.Services;

/// <summary>
/// HTTP client for Keycloak admin and OIDC operations
/// </summary>
public interface IKeycloakHttpClient
{
    Task<T?> GetAsync<T>(string endpoint, CancellationToken cancellationToken = default);
    Task<TResponse?> PostAsync<TRequest, TResponse>(string endpoint, TRequest request, CancellationToken cancellationToken = default);
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);
    Task<OidcDiscoveryDocument?> GetDiscoveryDocumentAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of Keycloak HTTP client with retry and error handling
/// </summary>
public class KeycloakHttpClient : IKeycloakHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly KeycloakOptions _options;
    private readonly ILogger<KeycloakHttpClient> _logger;

    public KeycloakHttpClient(
        HttpClient httpClient,
        IOptions<KeycloakOptions> options,
        ILogger<KeycloakHttpClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        // Configure HTTP client
        _httpClient.BaseAddress = new Uri(_options.Authority);
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.Connection.TimeoutSeconds);
    }

    public async Task<T?> GetAsync<T>(string endpoint, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(endpoint, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed for GET {Endpoint}", endpoint);
            return default;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize response from {Endpoint}", endpoint);
            return default;
        }
    }

    public async Task<TResponse?> PostAsync<TRequest, TResponse>(
        string endpoint,
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<TResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed for POST {Endpoint}", endpoint);
            return default;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to serialize/deserialize for {Endpoint}", endpoint);
            return default;
        }
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Try the discovery endpoint as a health check
            var discoveryUrl = _options.GetDiscoveryEndpoint();
            var response = await _httpClient.GetAsync(discoveryUrl, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Keycloak health check failed");
            return false;
        }
    }

    public async Task<OidcDiscoveryDocument?> GetDiscoveryDocumentAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var discoveryUrl = _options.GetDiscoveryEndpoint();
            _logger.LogDebug("Fetching OIDC discovery document from {Url}", discoveryUrl);

            var response = await _httpClient.GetAsync(discoveryUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var document = JsonSerializer.Deserialize<OidcDiscoveryDocument>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (document != null)
            {
                _logger.LogInformation("Successfully retrieved OIDC discovery document from {Issuer}", document.Issuer);
            }

            return document;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve OIDC discovery document");
            return null;
        }
    }
}

/// <summary>
/// OIDC Discovery Document model
/// </summary>
public class OidcDiscoveryDocument
{
    public string Issuer { get; set; } = string.Empty;
    public string AuthorizationEndpoint { get; set; } = string.Empty;
    public string TokenEndpoint { get; set; } = string.Empty;
    public string UserinfoEndpoint { get; set; } = string.Empty;
    public string JwksUri { get; set; } = string.Empty;
    public string IntrospectionEndpoint { get; set; } = string.Empty;
    public string EndSessionEndpoint { get; set; } = string.Empty;
    public string[] ResponseTypesSupported { get; set; } = Array.Empty<string>();
    public string[] GrantTypesSupported { get; set; } = Array.Empty<string>();
    public string[] ScopesSupported { get; set; } = Array.Empty<string>();
    public string[] TokenEndpointAuthMethodsSupported { get; set; } = Array.Empty<string>();
    public string[] IdTokenSigningAlgValuesSupported { get; set; } = Array.Empty<string>();
    public string[] ClaimsSupported { get; set; } = Array.Empty<string>();
}
