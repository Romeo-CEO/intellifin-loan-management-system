namespace IntelliFin.CreditAssessmentService.Services.Integration;

public interface ITransUnionClient
{
    Task<CreditBureauData?> GetCreditReportAsync(string nrc, Guid clientId, CancellationToken cancellationToken = default);
}

public class CreditBureauData
{
    public string Nrc { get; set; } = string.Empty;
    public decimal CreditScore { get; set; }
    public int TotalAccounts { get; set; }
    public int ActiveAccounts { get; set; }
    public int DefaultedAccounts { get; set; }
    public decimal TotalDebt { get; set; }
    public decimal MonthlyObligations { get; set; }
    public DateTime ReportDate { get; set; }
    public List<CreditAccount> Accounts { get; set; } = new();
}

public class CreditAccount
{
    public string AccountNumber { get; set; } = string.Empty;
    public string LenderName { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public decimal CurrentBalance { get; set; }
    public decimal CreditLimit { get; set; }
    public string PaymentHistory { get; set; } = string.Empty;
    public int DaysInArrears { get; set; }
}
