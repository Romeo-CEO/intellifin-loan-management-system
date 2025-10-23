using IntelliFin.CreditAssessmentService.Services.Models;

namespace IntelliFin.CreditAssessmentService.Services.Interfaces;

/// <summary>
/// Accesses PMEC employment verification and payroll information.
/// </summary>
public interface IPmecClient
{
    Task<PmecEmploymentProfile> GetEmploymentProfileAsync(Guid clientId, CancellationToken cancellationToken = default);
}
