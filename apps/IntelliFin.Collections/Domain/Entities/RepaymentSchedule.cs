namespace IntelliFin.Collections.Domain.Entities;

/// <summary>
/// Represents the complete repayment schedule for a loan.
/// Immutable once generated per BoZ requirements.
/// </summary>
public class RepaymentSchedule
{
    public Guid Id { get; set; }
    public Guid LoanId { get; set; }
    public Guid ClientId { get; set; }
    
    public string ProductCode { get; set; } = string.Empty;
    public decimal PrincipalAmount { get; set; }
    public decimal InterestRate { get; set; }
    public int TermMonths { get; set; }
    
    public string RepaymentFrequency { get; set; } = "Monthly"; // Monthly, BiWeekly, etc.
    public DateTime FirstPaymentDate { get; set; }
    public DateTime MaturityDate { get; set; }
    
    public DateTime GeneratedAt { get; set; }
    public string GeneratedBy { get; set; } = string.Empty;
    
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    
    // Audit fields
    public string? CorrelationId { get; set; }
    public string? WorkflowInstanceId { get; set; }
    
    // Navigation
    public ICollection<Installment> Installments { get; set; } = new List<Installment>();
}
