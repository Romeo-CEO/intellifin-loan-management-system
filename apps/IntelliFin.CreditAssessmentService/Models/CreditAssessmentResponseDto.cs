using IntelliFin.CreditAssessmentService.Domain.Enums;

namespace IntelliFin.CreditAssessmentService.Models;

/// <summary>
/// Represents the response returned after performing an assessment.
/// </summary>
public sealed class CreditAssessmentResponseDto
{
    public Guid AssessmentId { get; init; }
    public Guid LoanApplicationId { get; init; }
    public Guid ClientId { get; init; }
    public DateTime AssessedAt { get; init; }
    public RiskGrade RiskGrade { get; init; }
    public AssessmentDecision Decision { get; init; }
    public decimal CreditScore { get; init; }
    public decimal DebtToIncomeRatio { get; init; }
    public decimal PaymentCapacity { get; init; }
    public string VaultConfigVersion { get; init; } = string.Empty;
    public IReadOnlyCollection<AssessmentFactorDto> Factors { get; init; } = Array.Empty<AssessmentFactorDto>();
    public IReadOnlyCollection<AuditEntryDto> AuditTrail { get; init; } = Array.Empty<AuditEntryDto>();
}

public sealed class AssessmentFactorDto
{
    public string Name { get; init; } = string.Empty;
    public string Impact { get; init; } = string.Empty;
    public decimal Weight { get; init; }
    public decimal Contribution { get; init; }
    public string Explanation { get; init; } = string.Empty;
}

public sealed class AuditEntryDto
{
    public DateTime OccurredAt { get; init; }
    public string Actor { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string Details { get; init; } = string.Empty;
}
