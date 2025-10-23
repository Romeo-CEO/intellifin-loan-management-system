using IntelliFin.CreditAssessmentService.Domain.Entities;

namespace IntelliFin.CreditAssessmentService.Services.Interfaces;

/// <summary>
/// Publishes audit trail events to AdminService.
/// </summary>
public interface IAuditTrailPublisher
{
    Task PublishAsync(CreditAssessment assessment, string action, string details, CancellationToken cancellationToken = default);
}
