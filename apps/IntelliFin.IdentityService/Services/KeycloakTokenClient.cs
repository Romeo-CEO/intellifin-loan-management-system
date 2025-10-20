using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using IntelliFin.IdentityService.Configuration;
using IntelliFin.IdentityService.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntelliFin.IdentityService.Services;

public class KeycloakTokenClient : IKeycloakTokenClient
{
    private readonly HttpClient _httpClient;
    private readonly ServiceAccountConfiguration _configuration;
    private readonly ILogger<KeycloakTokenClient> _logger;

    public KeycloakTokenClient(
        HttpClient httpClient,
        IOptions<ServiceAccountConfiguration> configuration,
        ILogger<KeycloakTokenClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration.Value;
        _logger = logger;
    }

    public async Task<KeycloakTokenResponse> RequestClientCredentialsTokenAsync(
        string clientId,
        string clientSecret,
        IEnumerable<string>? scopes,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_configuration.KeycloakBaseUrl) || string.IsNullOrWhiteSpace(_configuration.KeycloakRealm))
        {
            throw new InvalidOperationException("Keycloak configuration is missing base URL or realm.");
        }

        var request = new HttpRequestMessage(HttpMethod.Post, BuildTokenEndpoint());
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var formValues = new List<KeyValuePair<string, string>>
        {
            new("grant_type", "client_credentials"),
            new("client_id", clientId),
            new("client_secret", clientSecret)
        };

        if (scopes is not null)
        {
            var scopeValue = string.Join(' ', scopes);
            if (!string.IsNullOrWhiteSpace(scopeValue))
            {
                formValues.Add(new KeyValuePair<string, string>("scope", scopeValue));
            }
        }

        request.Content = new FormUrlEncodedContent(formValues);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error calling Keycloak token endpoint for client {ClientId}", clientId);
            throw new KeycloakTokenException(HttpStatusCode.BadGateway, ex.Message, ex);
        }

        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            _logger.LogWarning(
                "Keycloak rejected client credentials for {ClientId} with status {StatusCode}",
                clientId,
                response.StatusCode);
            throw new UnauthorizedAccessException("Invalid client credentials.");
        }

        if ((int)response.StatusCode >= 500)
        {
            var body = await SafeReadBodyAsync(response, cancellationToken).ConfigureAwait(false);
            _logger.LogError(
                "Keycloak token endpoint returned {StatusCode} for {ClientId}. Body={Body}",
                response.StatusCode,
                clientId,
                body);
            throw new KeycloakTokenException(response.StatusCode, body);
        }

        response.EnsureSuccessStatusCode();

        var payload = await response.Content
            .ReadFromJsonAsync<KeycloakTokenResponse>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (payload is null || string.IsNullOrWhiteSpace(payload.AccessToken))
        {
            throw new KeycloakTokenException(HttpStatusCode.BadGateway, "Keycloak returned an empty token response.");
        }

        return payload;
    }

    private Uri BuildTokenEndpoint()
    {
        var baseUri = new Uri(AppendTrailingSlash(_configuration.KeycloakBaseUrl!), UriKind.Absolute);
        var builder = new StringBuilder();
        builder.Append("realms/")
            .Append(_configuration.KeycloakRealm!.Trim('/'))
            .Append("/protocol/openid-connect/token");

        return new Uri(baseUri, builder.ToString());
    }

    private static string AppendTrailingSlash(string value)
    {
        return value.EndsWith('/') ? value : value + "/";
    }

    private static async Task<string> SafeReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return string.Empty;
        }
    }
}
