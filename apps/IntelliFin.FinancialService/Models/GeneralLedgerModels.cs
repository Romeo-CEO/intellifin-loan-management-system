namespace IntelliFin.FinancialService.Models;

public class GLAccount
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AccountType Type { get; set; }
    public int? ParentId { get; set; }
    public string BranchId { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public decimal Balance { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class JournalEntry
{
    public int Id { get; set; }
    public int DebitAccountId { get; set; }
    public int CreditAccountId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "ZMW";
    public DateTime ValueDate { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

public class CreateJournalEntryRequest
{
    public int DebitAccountId { get; set; }
    public int CreditAccountId { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public DateTime? ValueDate { get; set; }
}

public class JournalEntryResult
{
    public bool Success { get; set; }
    public int? JournalEntryId { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> Errors { get; set; } = new();
}

public class TrialBalanceReport
{
    public DateTime AsOfDate { get; set; }
    public List<TrialBalanceItem> Items { get; set; } = new();
    public decimal TotalDebits { get; set; }
    public decimal TotalCredits { get; set; }
    public bool IsBalanced => TotalDebits == TotalCredits;
}

public class TrialBalanceItem
{
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public decimal DebitBalance { get; set; }
    public decimal CreditBalance { get; set; }
}

public class BoZReport
{
    public DateTime ReportDate { get; set; }
    public string ReportType { get; set; } = string.Empty;
    public Dictionary<string, decimal> Balances { get; set; } = new();
    public List<string> ComplianceNotes { get; set; } = new();
}

public enum AccountType
{
    Asset,
    Liability,
    Equity,
    Revenue,
    Expense
}
