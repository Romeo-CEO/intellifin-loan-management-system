using IntelliFin.CreditAssessmentService.Domain.Enums;

namespace IntelliFin.CreditAssessmentService.Domain.Events;

/// <summary>
/// Domain event emitted when an assessment is completed.
/// </summary>
public record AssessmentCompletedEvent(
    Guid AssessmentId,
    Guid LoanApplicationId,
    Guid ClientId,
    RiskGrade RiskGrade,
    AssessmentDecision Decision,
    decimal CreditScore,
    string VaultConfigVersion,
    DateTime CompletedAt,
    IReadOnlyCollection<string> KeyFactors
);
