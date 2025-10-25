namespace IntelliFin.CreditAssessmentService.Services.Integration;

public class ClientManagementClient : IClientManagementClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ClientManagementClient> _logger;

    public ClientManagementClient(HttpClient httpClient, ILogger<ClientManagementClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ClientKycData?> GetKycDataAsync(Guid clientId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching KYC data for client {ClientId}", clientId);
        
        try
        {
            // TODO: Implement actual HTTP call to Client Management Service
            // var response = await _httpClient.GetAsync($"/api/v1/clients/{clientId}/kyc", cancellationToken);
            
            // Stub response for now
            return await Task.FromResult(new ClientKycData
            {
                ClientId = clientId,
                IsVerified = true,
                VerificationDate = DateTime.UtcNow.AddDays(-30),
                ExpiryDate = DateTime.UtcNow.AddYears(1),
                VerificationStatus = "Verified"
            });
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
        
        try
        {
            // TODO: Implement actual HTTP call
            return await Task.FromResult(new ClientEmploymentData
            {
                ClientId = clientId,
                EmployerName = "Government of Zambia",
                MonthlyIncome = 15000,
                EmploymentMonths = 24,
                EmploymentType = "Permanent"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching employment data for client {ClientId}", clientId);
            return null;
        }
    }
}
