using IntelliFin.CreditAssessmentService.Domain.Enums;
using IntelliFin.CreditAssessmentService.Domain.ValueObjects;

namespace IntelliFin.CreditAssessmentService.Domain.Entities;

/// <summary>
/// Represents the outcome of a credit assessment.
/// </summary>
public class CreditAssessment
{
    public Guid Id { get; set; }
    public Guid LoanApplicationId { get; set; }
    public Guid ClientId { get; set; }
    public DateTime AssessedAt { get; set; }
    public string AssessedBy { get; set; } = string.Empty;
    public AssessmentStatus Status { get; set; }
    public RiskGrade RiskGrade { get; set; }
    public decimal CreditScore { get; set; }
    public decimal DebtToIncomeRatio { get; set; }
    public decimal PaymentCapacity { get; set; }
    public AssessmentDecision Decision { get; set; }
    public string DecisionReason { get; set; } = string.Empty;
    public string VaultConfigVersion { get; set; } = string.Empty;
    public bool HasCreditBureauData { get; set; }
    public ICollection<AssessmentFactor> Factors { get; set; } = new List<AssessmentFactor>();
    public ICollection<ManualOverride> Overrides { get; set; } = new List<ManualOverride>();
    public ICollection<AssessmentAuditTrail> AuditTrail { get; set; } = new List<AssessmentAuditTrail>();
}
