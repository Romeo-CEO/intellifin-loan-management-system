using System.Net.Http.Json;
using IntelliFin.CreditAssessmentService.Options;
using IntelliFin.CreditAssessmentService.Services.Interfaces;
using IntelliFin.CreditAssessmentService.Services.Models;
using Microsoft.Extensions.Options;

namespace IntelliFin.CreditAssessmentService.Infrastructure.External;

/// <summary>
/// HTTP client responsible for interacting with the TransUnion credit bureau.
/// </summary>
public sealed class TransUnionClient : ITransUnionClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TransUnionClient> _logger;
    private readonly TransUnionOptions _options;

    public TransUnionClient(HttpClient httpClient, IOptions<TransUnionOptions> options, ILogger<TransUnionClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<TransUnionReport> GetReportAsync(Guid clientId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/credit-reports/{clientId}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("TransUnion returned {StatusCode} for client {ClientId}. Falling back to empty report.", response.StatusCode, clientId);
                return new TransUnionReport { IsAvailable = false };
            }

            var report = await response.Content.ReadFromJsonAsync<TransUnionReport>(cancellationToken: cancellationToken);
            if (report is null)
            {
                _logger.LogWarning("TransUnion returned null report for client {ClientId}", clientId);
                return new TransUnionReport { IsAvailable = false };
            }

            return report with { IsAvailable = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving TransUnion report for client {ClientId}", clientId);
            return new TransUnionReport { IsAvailable = false };
        }
    }
}
