namespace IntelliFin.Collections.Domain.Entities;

/// <summary>
/// Represents a single installment in a repayment schedule.
/// Immutable once generated per BoZ requirements.
/// </summary>
public class Installment
{
    public Guid Id { get; set; }
    public Guid RepaymentScheduleId { get; set; }
    
    public int InstallmentNumber { get; set; }
    public DateTime DueDate { get; set; }
    
    public decimal PrincipalDue { get; set; }
    public decimal InterestDue { get; set; }
    public decimal TotalDue { get; set; }
    
    public decimal PrincipalPaid { get; set; }
    public decimal InterestPaid { get; set; }
    public decimal TotalPaid { get; set; }
    
    public decimal PrincipalBalance { get; set; }
    
    public string Status { get; set; } = "Pending"; // Pending, Paid, PartiallyPaid, Overdue
    
    public DateTime? PaidDate { get; set; }
    public int DaysPastDue { get; set; }
    
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    
    // Navigation
    public RepaymentSchedule RepaymentSchedule { get; set; } = null!;
    public ICollection<PaymentTransaction> Payments { get; set; } = new List<PaymentTransaction>();
}
