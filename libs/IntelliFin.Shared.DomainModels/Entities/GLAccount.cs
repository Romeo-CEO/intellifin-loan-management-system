namespace IntelliFin.Shared.DomainModels.Entities;

public class GLAccount
{
    public Guid Id { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // Asset, Liability, Equity, Income, Expense
    public bool IsActive { get; set; } = true;
}

