namespace IntelliFin.Shared.DomainModels.Entities;

public class LoanApplication
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public decimal Amount { get; set; }
    public int TermMonths { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public string ApplicationDataJson { get; set; } = "{}";
    public decimal RequestedAmount { get; set; }
    public string? WorkflowInstanceId { get; set; }
    public string? DeclineReason { get; set; }

    public Client? Client { get; set; }
    public LoanProduct? Product { get; set; }
    public ICollection<CreditAssessment> CreditAssessments { get; set; } = new List<CreditAssessment>();
}

