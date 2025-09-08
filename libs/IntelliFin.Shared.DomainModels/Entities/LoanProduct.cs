namespace IntelliFin.Shared.DomainModels.Entities;

public class LoanProduct
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal MinAmount { get; set; }
    public decimal MaxAmount { get; set; }
    public int MinTermMonths { get; set; }
    public int MaxTermMonths { get; set; }
    public decimal BaseInterestRate { get; set; }
    public decimal InterestRateAnnualPercent { get; set; }
    public int TermMonthsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string Category { get; set; } = string.Empty;

    public ICollection<ApplicationField> RequiredFields { get; set; } = new List<ApplicationField>();
    public ICollection<ValidationRule> ValidationRules { get; set; } = new List<ValidationRule>();
    public ICollection<LoanApplication> LoanApplications { get; set; } = new List<LoanApplication>();
}

