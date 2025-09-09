namespace IntelliFin.Shared.DomainModels.Entities;

public class GLEntry
{
    public Guid Id { get; set; }
    public string EntryNumber { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "Posted";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public string BatchId { get; set; } = string.Empty;

    public ICollection<GLEntryLine> Lines { get; set; } = new List<GLEntryLine>();
}

public class GLEntryLine
{
    public Guid Id { get; set; }
    public Guid GLEntryId { get; set; }
    public Guid GLAccountId { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public GLEntry? GLEntry { get; set; }
    public GLAccount? GLAccount { get; set; }
}

public class GLBalance
{
    public Guid Id { get; set; }
    public Guid GLAccountId { get; set; }
    public int PeriodYear { get; set; }
    public int PeriodMonth { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal DebitTotal { get; set; }
    public decimal CreditTotal { get; set; }
    public decimal ClosingBalance { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public bool IsLocked { get; set; }

    public GLAccount? GLAccount { get; set; }
}