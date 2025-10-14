using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using IntelliFin.AdminService.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntelliFin.AdminService.Services;

public sealed class CamundaTokenProvider : ICamundaTokenProvider, IDisposable
{
    private const string ClientCredentialsGrantType = "client_credentials";

    private static readonly Meter Meter = new("IntelliFin.AdminService.Camunda.Token", "1.0.0");
    private static readonly Histogram<double> TokenRefreshDuration = Meter.CreateHistogram<double>("camunda.token.refresh.duration", unit: "ms");

    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<CamundaOptions> _optionsMonitor;
    private readonly ILogger<CamundaTokenProvider> _logger;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    private string? _accessToken;
    private DateTimeOffset _expiresAt;
    private int _refreshBufferSeconds;

    public CamundaTokenProvider(
        HttpClient httpClient,
        IOptionsMonitor<CamundaOptions> optionsMonitor,
        ILogger<CamundaTokenProvider> logger)
    {
        _httpClient = httpClient;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.CurrentValue;
        if (options.FailOpen && string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            throw new InvalidOperationException("Camunda token acquisition requested while FailOpen is enabled and Camunda is disabled.");
        }

        await _tokenLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (TokenIsValid(options))
            {
                return _accessToken!;
            }

            var requestContent = new FormUrlEncodedContent(BuildRequestBody(options));
            var stopwatch = Stopwatch.StartNew();
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, string.Empty)
            {
                Content = requestContent
            };
            requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _httpClient.SendAsync(requestMessage, cancellationToken).ConfigureAwait(false);
            var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Failed to acquire Camunda access token. Status={StatusCode} Body={Body}",
                    (int)response.StatusCode,
                    payload);

                throw new InvalidOperationException($"Failed to acquire Camunda token. Status {(int)response.StatusCode}.");
            }

            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (string.IsNullOrWhiteSpace(tokenResponse?.AccessToken))
            {
                throw new InvalidOperationException("Camunda token endpoint returned an empty access token.");
            }

            _accessToken = tokenResponse.AccessToken;
            var expiresIn = tokenResponse.ExpiresIn > 0 ? tokenResponse.ExpiresIn : 60;
            _refreshBufferSeconds = Math.Clamp(options.TokenRefreshBufferSeconds, 0, Math.Max(expiresIn - 1, 0));
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn);

            _logger.LogInformation("Camunda access token acquired. ExpiresAt={ExpiresAt}", _expiresAt);

            stopwatch.Stop();
            TokenRefreshDuration.Record(stopwatch.Elapsed.TotalMilliseconds);

            return _accessToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private bool TokenIsValid(CamundaOptions options)
    {
        var buffer = Math.Clamp(_refreshBufferSeconds, 0, 300);
        return !string.IsNullOrEmpty(_accessToken) && _expiresAt > DateTimeOffset.UtcNow.AddSeconds(buffer);
    }

    private static IEnumerable<KeyValuePair<string, string>> BuildRequestBody(CamundaOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ClientId) || string.IsNullOrWhiteSpace(options.ClientSecret))
        {
            throw new InvalidOperationException("Camunda client credentials are not configured.");
        }

        var body = new List<KeyValuePair<string, string>>
        {
            new("grant_type", ClientCredentialsGrantType),
            new("client_id", options.ClientId),
            new("client_secret", options.ClientSecret)
        };

        if (!string.IsNullOrWhiteSpace(options.Scope))
        {
            body.Add(new KeyValuePair<string, string>("scope", options.Scope));
        }

        return body;
    }

    public void Dispose()
    {
        _tokenLock.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed record TokenResponse(string? AccessToken, int ExpiresIn);
}
