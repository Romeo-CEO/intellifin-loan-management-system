namespace IntelliFin.Shared.DomainModels.Entities;

public class CreditAssessment
{
    public Guid Id { get; set; }
    public Guid LoanApplicationId { get; set; }
    public string RiskGrade { get; set; } = string.Empty;
    public decimal CreditScore { get; set; }
    public decimal DebtToIncomeRatio { get; set; }
    public decimal PaymentCapacity { get; set; }
    public bool HasCreditBureauData { get; set; }
    public string ScoreExplanation { get; set; } = string.Empty;
    public DateTime AssessedAt { get; set; } = DateTime.UtcNow;
    public string AssessedBy { get; set; } = string.Empty;

    public LoanApplication? LoanApplication { get; set; }
    public ICollection<CreditFactor> CreditFactors { get; set; } = new List<CreditFactor>();
    public ICollection<RiskIndicator> RiskIndicators { get; set; } = new List<RiskIndicator>();
}

public class CreditFactor
{
    public Guid Id { get; set; }
    public Guid CreditAssessmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal Score { get; set; }
    public string Impact { get; set; } = string.Empty;

    public CreditAssessment? CreditAssessment { get; set; }
}

public class RiskIndicator
{
    public Guid Id { get; set; }
    public Guid CreditAssessmentId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public decimal Impact { get; set; }

    public CreditAssessment? CreditAssessment { get; set; }
}