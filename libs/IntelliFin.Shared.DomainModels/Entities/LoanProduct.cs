namespace IntelliFin.Shared.DomainModels.Entities;

public class LoanProduct
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal InterestRateAnnualPercent { get; set; }
    public int TermMonthsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

