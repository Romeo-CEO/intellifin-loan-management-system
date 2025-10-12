using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using IntelliFin.UserMigration.Options;

namespace IntelliFin.UserMigration.Services;

public sealed class KeycloakTokenService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<KeycloakTokenService> _logger;
    private readonly KeycloakOptions _options;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private string? _token;
    private DateTimeOffset _expiresAt;

    public KeycloakTokenService(IHttpClientFactory httpClientFactory, IOptions<KeycloakOptions> options, ILogger<KeycloakTokenService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_token is not null && DateTimeOffset.UtcNow < _expiresAt)
        {
            return _token;
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_token is not null && DateTimeOffset.UtcNow < _expiresAt)
            {
                return _token;
            }

            var client = _httpClientFactory.CreateClient("keycloak-token");
            var form = new Dictionary<string, string>
            {
                ["grant_type"] = _options.UseClientCredentials ? "client_credentials" : "password",
                ["client_id"] = _options.ClientId,
            };

            if (_options.UseClientCredentials)
            {
                if (string.IsNullOrEmpty(_options.ClientSecret))
                {
                    throw new InvalidOperationException("Client secret must be configured for client credentials flow.");
                }

                form["client_secret"] = _options.ClientSecret;
            }
            else
            {
                if (string.IsNullOrEmpty(_options.Username) || string.IsNullOrEmpty(_options.Password))
                {
                    throw new InvalidOperationException("Username and password must be configured for resource owner password flow.");
                }

                form["username"] = _options.Username;
                form["password"] = _options.Password;
            }

            var tokenRealm = string.IsNullOrWhiteSpace(_options.TokenRealm) ? _options.Realm : _options.TokenRealm;
            var response = await client.PostAsync($"realms/{tokenRealm}/protocol/openid-connect/token", new FormUrlEncodedContent(form), cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!json.RootElement.TryGetProperty("access_token", out var tokenElement))
            {
                throw new InvalidOperationException("Keycloak token response did not contain an access token.");
            }

            _token = tokenElement.GetString();
            var expiresIn = json.RootElement.TryGetProperty("expires_in", out var expiresElement)
                ? expiresElement.GetInt32()
                : 60;
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(expiresIn - 30, 30));

            _logger.LogInformation("Obtained Keycloak admin token valid until {ExpiresAt}", _expiresAt);
            return _token!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to retrieve Keycloak admin token.");
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Invalidate() => _token = null;
}
