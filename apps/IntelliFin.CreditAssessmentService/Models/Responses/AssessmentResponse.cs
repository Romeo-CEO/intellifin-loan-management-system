namespace IntelliFin.CreditAssessmentService.Models.Responses;

/// <summary>
/// Response model for credit assessment result.
/// </summary>
public class AssessmentResponse
{
    /// <summary>
    /// Unique identifier of the created assessment.
    /// </summary>
    public Guid AssessmentId { get; set; }

    /// <summary>
    /// Loan application ID that was assessed.
    /// </summary>
    public Guid LoanApplicationId { get; set; }

    /// <summary>
    /// Client ID for whom the assessment was performed.
    /// </summary>
    public Guid ClientId { get; set; }

    /// <summary>
    /// Assessment decision: Approved, Conditional, ManualReview, Rejected.
    /// </summary>
    public string Decision { get; set; } = string.Empty;

    /// <summary>
    /// Risk grade: A, B, C, D, F.
    /// </summary>
    public string RiskGrade { get; set; } = string.Empty;

    /// <summary>
    /// Composite risk score (0-1000).
    /// </summary>
    public decimal CompositeScore { get; set; }

    /// <summary>
    /// Credit bureau score (if available).
    /// </summary>
    public decimal? CreditScore { get; set; }

    /// <summary>
    /// Debt-to-income ratio (0.0 - 1.0).
    /// </summary>
    public decimal DebtToIncomeRatio { get; set; }

    /// <summary>
    /// Maximum affordable loan amount based on analysis.
    /// </summary>
    public decimal AffordableAmount { get; set; }

    /// <summary>
    /// Maximum affordable monthly payment.
    /// </summary>
    public decimal AffordablePayment { get; set; }

    /// <summary>
    /// List of rules that were evaluated.
    /// </summary>
    public List<RuleEvaluationDto> RulesFired { get; set; } = new();

    /// <summary>
    /// Conditions that must be met if decision is Conditional.
    /// </summary>
    public List<string>? Conditions { get; set; }

    /// <summary>
    /// Human-readable explanation of the decision.
    /// </summary>
    public string Explanation { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when assessment was performed.
    /// </summary>
    public DateTime AssessedAt { get; set; }

    /// <summary>
    /// User ID who initiated the assessment.
    /// </summary>
    public Guid? AssessedByUserId { get; set; }

    /// <summary>
    /// Vault configuration version used for assessment.
    /// </summary>
    public string? VaultConfigVersion { get; set; }

    /// <summary>
    /// Indicates if the assessment is currently valid.
    /// </summary>
    public bool IsValid { get; set; } = true;

    /// <summary>
    /// Reason if assessment was invalidated.
    /// </summary>
    public string? InvalidReason { get; set; }
}
