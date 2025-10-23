namespace IntelliFin.CreditAssessmentService.Services.Models;

/// <summary>
/// Representation of the data retrieved from TransUnion.
/// </summary>
public sealed class TransUnionReport
{
    public bool IsAvailable { get; init; }
    public decimal CreditScore { get; init; }
    public int TotalAccounts { get; init; }
    public int ActiveAccounts { get; init; }
    public int DelinquentAccounts { get; init; }
    public decimal TotalDebt { get; init; }
    public decimal MonthlyObligations { get; init; }
    public IReadOnlyCollection<TransUnionTradeLine> Trades { get; init; } = Array.Empty<TransUnionTradeLine>();
    public IReadOnlyCollection<string> Alerts { get; init; } = Array.Empty<string>();
}

public sealed class TransUnionTradeLine
{
    public string AccountNumber { get; init; } = string.Empty;
    public string LenderName { get; init; } = string.Empty;
    public string AccountType { get; init; } = string.Empty;
    public decimal CurrentBalance { get; init; }
    public decimal CreditLimit { get; init; }
    public string Status { get; init; } = string.Empty;
    public int DaysPastDue { get; init; }
}
