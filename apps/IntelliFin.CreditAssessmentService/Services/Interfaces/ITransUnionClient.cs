using IntelliFin.CreditAssessmentService.Services.Models;

namespace IntelliFin.CreditAssessmentService.Services.Interfaces;

/// <summary>
/// Provides access to TransUnion credit bureau data.
/// </summary>
public interface ITransUnionClient
{
    Task<TransUnionReport> GetReportAsync(Guid clientId, CancellationToken cancellationToken = default);
}
