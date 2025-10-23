using IntelliFin.CreditAssessmentService.Domain.Enums;

namespace IntelliFin.CreditAssessmentService.Services.Models;

/// <summary>
/// Contextual information required for rule evaluation.
/// </summary>
public sealed class RuleEvaluationContext
{
    public Guid LoanApplicationId { get; init; }
    public Guid ClientId { get; init; }
    public decimal RequestedAmount { get; init; }
    public int TermMonths { get; init; }
    public decimal InterestRate { get; init; }
    public decimal MonthlyIncome { get; init; }
    public decimal MonthlyExpenses { get; init; }
    public decimal ExistingDebtPayments { get; init; }
    public decimal DebtToIncomeRatio { get; init; }
    public decimal BureauScore { get; init; }
    public bool HasBureauData { get; init; }
    public IReadOnlyCollection<string> RiskFlags { get; init; } = Array.Empty<string>();
    public IReadOnlyDictionary<string, decimal> FinancialMetrics { get; init; } = new Dictionary<string, decimal>();
}
