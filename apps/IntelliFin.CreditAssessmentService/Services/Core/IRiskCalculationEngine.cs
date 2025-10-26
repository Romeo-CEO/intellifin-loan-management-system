using IntelliFin.CreditAssessmentService.Models.Responses;

namespace IntelliFin.CreditAssessmentService.Services.Core;

/// <summary>
/// Risk calculation engine interface.
/// </summary>
public interface IRiskCalculationEngine
{
    /// <summary>
    /// Calculates risk score and grade based on assessment data.
    /// </summary>
    Task<RiskCalculationResult> CalculateRiskAsync(
        AssessmentData assessmentData,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Assessment data for risk calculation.
/// </summary>
public class AssessmentData
{
    public decimal RequestedAmount { get; set; }
    public int TermMonths { get; set; }
    public string ProductType { get; set; } = string.Empty;
    public decimal MonthlyIncome { get; set; }
    public decimal ExistingDebt { get; set; }
    public decimal? CreditScore { get; set; }
    public int EmploymentMonths { get; set; }
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}

/// <summary>
/// Result of risk calculation.
/// </summary>
public class RiskCalculationResult
{
    public string Grade { get; set; } = string.Empty;
    public decimal Score { get; set; }
    public decimal DebtToIncomeRatio { get; set; }
    public decimal AffordableAmount { get; set; }
    public decimal AffordablePayment { get; set; }
    public List<RuleEvaluationDto> RulesFired { get; set; } = new();
    public string Decision { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
}
