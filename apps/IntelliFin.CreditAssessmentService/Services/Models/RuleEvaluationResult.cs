using IntelliFin.CreditAssessmentService.Domain.Enums;
using IntelliFin.CreditAssessmentService.Domain.ValueObjects;

namespace IntelliFin.CreditAssessmentService.Services.Models;

/// <summary>
/// Represents the outcome of rule evaluation.
/// </summary>
public sealed class RuleEvaluationResult
{
    public RiskGrade RiskGrade { get; init; }
    public AssessmentDecision Decision { get; init; }
    public decimal CreditScore { get; init; }
    public decimal DebtToIncomeRatio { get; init; }
    public decimal PaymentCapacity { get; init; }
    public IReadOnlyCollection<AssessmentFactor> Factors { get; init; } = Array.Empty<AssessmentFactor>();
    public IReadOnlyCollection<string> AuditMessages { get; init; } = Array.Empty<string>();
}
