using IntelliFin.CreditAssessmentService.Services.Models;

namespace IntelliFin.CreditAssessmentService.Services.Interfaces;

/// <summary>
/// Provides KYC and employment data from the client management service.
/// </summary>
public interface IClientManagementClient
{
    Task<ClientProfile> GetClientProfileAsync(Guid clientId, CancellationToken cancellationToken = default);
    Task<ClientFinancialProfile> GetFinancialProfileAsync(Guid clientId, CancellationToken cancellationToken = default);
}
