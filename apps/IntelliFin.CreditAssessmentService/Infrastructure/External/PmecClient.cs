using System.Net.Http.Json;
using IntelliFin.CreditAssessmentService.Options;
using IntelliFin.CreditAssessmentService.Services.Interfaces;
using IntelliFin.CreditAssessmentService.Services.Models;
using Microsoft.Extensions.Options;

namespace IntelliFin.CreditAssessmentService.Infrastructure.External;

/// <summary>
/// Client used for PMEC government employment verification.
/// </summary>
public sealed class PmecClient : IPmecClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PmecClient> _logger;

    public PmecClient(HttpClient httpClient, IOptions<PmecOptions> options, ILogger<PmecClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.Timeout = options.Value.Timeout;
    }

    public async Task<PmecEmploymentProfile> GetEmploymentProfileAsync(Guid clientId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/employment/{clientId}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("PMEC returned {StatusCode} for client {ClientId}. Returning unknown profile.", response.StatusCode, clientId);
                return new PmecEmploymentProfile { IsEmployed = false };
            }

            var profile = await response.Content.ReadFromJsonAsync<PmecEmploymentProfile>(cancellationToken: cancellationToken);
            return profile ?? new PmecEmploymentProfile { IsEmployed = false };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving PMEC employment profile for client {ClientId}", clientId);
            return new PmecEmploymentProfile { IsEmployed = false };
        }
    }
}
