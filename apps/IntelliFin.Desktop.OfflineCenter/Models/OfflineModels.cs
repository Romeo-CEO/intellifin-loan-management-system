namespace IntelliFin.Desktop.OfflineCenter.Models;

public class OfflineLoan
{
    public int Id { get; set; }
    public string LoanId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public decimal PrincipalAmount { get; set; }
    public decimal OutstandingBalance { get; set; }
    public decimal InterestRate { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime DisbursementDate { get; set; }
    public DateTime MaturityDate { get; set; }
    public int DaysPastDue { get; set; }
    public string BoZClassification { get; set; } = string.Empty;
    public decimal ProvisionAmount { get; set; }
    public DateTime LastSyncDate { get; set; }
    public bool IsSynced { get; set; }
}

public class OfflineClient
{
    public int Id { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string NationalId { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string Ministry { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public decimal MonthlySalary { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public DateTime LastSyncDate { get; set; }
    public bool IsSynced { get; set; }
}

public class OfflinePayment
{
    public int Id { get; set; }
    public string PaymentId { get; set; } = string.Empty;
    public string LoanId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; }
    public string ExternalReference { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTime LastSyncDate { get; set; }
    public bool IsSynced { get; set; }
}

public class OfflineFinancialSummary
{
    public int Id { get; set; }
    public DateTime SummaryDate { get; set; }
    public decimal TotalLoansOutstanding { get; set; }
    public decimal TotalCollections { get; set; }
    public decimal TotalDisbursements { get; set; }
    public decimal TotalProvisions { get; set; }
    public decimal CashBalance { get; set; }
    public int TotalActiveLoans { get; set; }
    public int TotalOverdueLoans { get; set; }
    public decimal AverageInterestRate { get; set; }
    public string Period { get; set; } = string.Empty; // Daily, Weekly, Monthly
    public DateTime LastSyncDate { get; set; }
    public bool IsSynced { get; set; }
}

public class OfflineSyncLog
{
    public int Id { get; set; }
    public string EntityType { get; set; } = string.Empty; // Loans, Clients, Payments, etc.
    public string EntityId { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty; // Create, Update, Delete, Sync
    public string Status { get; set; } = string.Empty; // Pending, Success, Failed
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string SyncDirection { get; set; } = string.Empty; // Upload, Download
    public int RetryCount { get; set; }
}

public class OfflineReport
{
    public int Id { get; set; }
    public string ReportId { get; set; } = string.Empty;
    public string ReportType { get; set; } = string.Empty; // BoZ, TrialBalance, Collections, etc.
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty; // JSON or XML content
    public DateTime GeneratedDate { get; set; }
    public DateTime ReportPeriodStart { get; set; }
    public DateTime ReportPeriodEnd { get; set; }
    public string Status { get; set; } = string.Empty; // Draft, Final, Submitted
    public DateTime LastSyncDate { get; set; }
    public bool IsSynced { get; set; }
}

public class DashboardSummary
{
    public decimal TotalPortfolioValue { get; set; }
    public int TotalActiveLoans { get; set; }
    public int TotalClients { get; set; }
    public decimal MonthlyCollections { get; set; }
    public decimal MonthlyDisbursements { get; set; }
    public int OverdueLoans { get; set; }
    public decimal OverdueAmount { get; set; }
    public decimal PortfolioAtRisk { get; set; }
    public decimal CashPosition { get; set; }
    public DateTime LastUpdateTime { get; set; }
    public bool IsOnline { get; set; }
}

public class LoanSummary
{
    public string LoanId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public decimal OutstandingBalance { get; set; }
    public int DaysPastDue { get; set; }
    public string Status { get; set; } = string.Empty;
    public string BoZClassification { get; set; } = string.Empty;
    public DateTime NextPaymentDate { get; set; }
    public decimal NextPaymentAmount { get; set; }
}
