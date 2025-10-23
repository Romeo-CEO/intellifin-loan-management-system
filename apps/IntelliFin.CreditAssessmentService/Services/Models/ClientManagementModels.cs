namespace IntelliFin.CreditAssessmentService.Services.Models;

/// <summary>
/// Represents the high-level client profile obtained from client management.
/// </summary>
public sealed class ClientProfile
{
    public Guid ClientId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string NationalId { get; init; } = string.Empty;
    public bool IsKycComplete { get; init; }
    public IReadOnlyCollection<string> RiskFlags { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Represents financial profile details used during affordability analysis.
/// </summary>
public sealed class ClientFinancialProfile
{
    public decimal MonthlyIncome { get; init; }
    public decimal MonthlyExpenses { get; init; }
    public decimal ExistingDebtPayments { get; init; }
    public IReadOnlyDictionary<string, decimal> AdditionalMetrics { get; init; } = new Dictionary<string, decimal>();
}
