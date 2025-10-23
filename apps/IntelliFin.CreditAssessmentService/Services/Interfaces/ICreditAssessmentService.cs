using IntelliFin.CreditAssessmentService.Domain.Entities;
using IntelliFin.CreditAssessmentService.Models;

namespace IntelliFin.CreditAssessmentService.Services.Interfaces;

/// <summary>
/// Primary service responsible for coordinating credit assessments.
/// </summary>
public interface ICreditAssessmentService
{
    Task<CreditAssessment> AssessAsync(CreditAssessmentRequestDto request, CancellationToken cancellationToken = default);
    Task<CreditAssessment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<CreditAssessment>> GetByLoanApplicationAsync(Guid loanApplicationId, CancellationToken cancellationToken = default);
    Task<CreditAssessment> RecordManualOverrideAsync(Guid assessmentId, ManualOverrideRequestDto request, CancellationToken cancellationToken = default);
    Task InvalidateAsync(Guid assessmentId, string reason, CancellationToken cancellationToken = default);
}
