using System.Net.Http.Json;
using IntelliFin.CreditAssessmentService.Options;
using IntelliFin.CreditAssessmentService.Services.Interfaces;
using IntelliFin.CreditAssessmentService.Services.Models;
using Microsoft.Extensions.Options;

namespace IntelliFin.CreditAssessmentService.Infrastructure.External;

/// <summary>
/// Client for retrieving KYC and financial information from the Client Management API.
/// </summary>
public sealed class ClientManagementClient : IClientManagementClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ClientManagementClient> _logger;

    public ClientManagementClient(HttpClient httpClient, IOptions<ClientManagementOptions> options, ILogger<ClientManagementClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.Timeout = options.Value.Timeout;
    }

    public async Task<ClientProfile> GetClientProfileAsync(Guid clientId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/clients/{clientId}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("ClientManagement returned {StatusCode} for client {ClientId}", response.StatusCode, clientId);
                return new ClientProfile { ClientId = clientId, IsKycComplete = false };
            }

            return await response.Content.ReadFromJsonAsync<ClientProfile>(cancellationToken: cancellationToken)
                ?? new ClientProfile { ClientId = clientId, IsKycComplete = false };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving client profile for {ClientId}", clientId);
            return new ClientProfile { ClientId = clientId, IsKycComplete = false };
        }
    }

    public async Task<ClientFinancialProfile> GetFinancialProfileAsync(Guid clientId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/clients/{clientId}/financials", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("ClientManagement financial endpoint returned {StatusCode} for client {ClientId}", response.StatusCode, clientId);
                return new ClientFinancialProfile();
            }

            return await response.Content.ReadFromJsonAsync<ClientFinancialProfile>(cancellationToken: cancellationToken)
                ?? new ClientFinancialProfile();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving financial profile for {ClientId}", clientId);
            return new ClientFinancialProfile();
        }
    }
}
