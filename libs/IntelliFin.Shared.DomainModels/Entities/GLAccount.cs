namespace IntelliFin.Shared.DomainModels.Entities;

public class GLAccount
{
    public Guid Id { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // Asset, Liability, Equity, Income, Expense
    public string AccountType { get; set; } = string.Empty;
    public Guid? ParentAccountId { get; set; }
    public int Level { get; set; }
    public bool IsContraAccount { get; set; }
    public decimal CurrentBalance { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;

    public GLAccount? ParentAccount { get; set; }
    public ICollection<GLAccount> SubAccounts { get; set; } = new List<GLAccount>();
    public ICollection<GLEntryLine> GLEntryLines { get; set; } = new List<GLEntryLine>();
    public ICollection<GLBalance> GLBalances { get; set; } = new List<GLBalance>();
}

