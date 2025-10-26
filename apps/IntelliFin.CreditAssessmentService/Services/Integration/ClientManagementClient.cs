using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using Polly;
using Polly.Retry;

namespace IntelliFin.CreditAssessmentService.Services.Integration;

public class ClientManagementClient : IClientManagementClient
{
    private readonly HttpClient _httpClient;
    private readonly IDistributedCache? _cache;
    private readonly ILogger<ClientManagementClient> _logger;
    private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;

    public ClientManagementClient(
        HttpClient httpClient, 
        IDistributedCache? cache,
        ILogger<ClientManagementClient> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;

        // Retry policy for transient failures
        _retryPolicy = Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .OrResult(r => (int)r.StatusCode >= 500)
            .WaitAndRetryAsync(
                retryCount: 2,
                sleepDurationProvider: attempt => TimeSpan.FromMilliseconds(200 * attempt),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning("Client Management API retry {RetryCount} after {Delay}ms",
                        retryCount, timespan.TotalMilliseconds);
                });
    }

    public async Task<ClientKycData?> GetKycDataAsync(Guid clientId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching KYC data for client {ClientId}", clientId);
        
        // Check cache first
        var cacheKey = $"client:kyc:{clientId}";
        if (_cache != null)
        {
            var cachedData = await _cache.GetStringAsync(cacheKey, cancellationToken);
            if (!string.IsNullOrEmpty(cachedData))
            {
                _logger.LogDebug("KYC data found in cache for client {ClientId}", clientId);
                return JsonSerializer.Deserialize<ClientKycData>(cachedData);
            }
        }

        try
        {
            // Make actual HTTP call to Client Management Service
            var response = await _retryPolicy.ExecuteAsync(async () =>
            {
                return await _httpClient.GetAsync($"/api/v1/clients/{clientId}/kyc", cancellationToken);
            });

            if (response.IsSuccessStatusCode)
            {
                var kycData = await response.Content.ReadFromJsonAsync<ClientKycData>(cancellationToken);
                
                if (kycData != null && _cache != null)
                {
                    // Cache for 1 hour
                    var cacheOptions = new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
                    };
                    await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(kycData), cacheOptions, cancellationToken);
                }

                return kycData;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Client {ClientId} not found in Client Management Service", clientId);
                return null;
            }
            else
            {
                _logger.LogError("Client Management API returned {StatusCode} for client {ClientId}",
                    response.StatusCode, clientId);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching KYC data for client {ClientId}", clientId);
            return null;
        }
    }

    public async Task<ClientEmploymentData?> GetEmploymentDataAsync(Guid clientId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching employment data for client {ClientId}", clientId);
        
        // Check cache first
        var cacheKey = $"client:employment:{clientId}";
        if (_cache != null)
        {
            var cachedData = await _cache.GetStringAsync(cacheKey, cancellationToken);
            if (!string.IsNullOrEmpty(cachedData))
            {
                _logger.LogDebug("Employment data found in cache for client {ClientId}", clientId);
                return JsonSerializer.Deserialize<ClientEmploymentData>(cachedData);
            }
        }

        try
        {
            // Make actual HTTP call to Client Management Service
            var response = await _retryPolicy.ExecuteAsync(async () =>
            {
                return await _httpClient.GetAsync($"/api/v1/clients/{clientId}/employment", cancellationToken);
            });

            if (response.IsSuccessStatusCode)
            {
                var employmentData = await response.Content.ReadFromJsonAsync<ClientEmploymentData>(cancellationToken);
                
                if (employmentData != null && _cache != null)
                {
                    // Cache for 24 hours
                    var cacheOptions = new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
                    };
                    await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(employmentData), cacheOptions, cancellationToken);
                }

                return employmentData;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Employment data not found for client {ClientId}", clientId);
                return null;
            }
            else
            {
                _logger.LogError("Client Management API returned {StatusCode} for client {ClientId}",
                    response.StatusCode, clientId);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching employment data for client {ClientId}", clientId);
            return null;
        }
    }
}
