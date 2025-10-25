namespace IntelliFin.Collections.Domain.Entities;

/// <summary>
/// Represents a payment reconciliation task for manual review.
/// </summary>
public class ReconciliationTask
{
    public Guid Id { get; set; }
    public Guid PaymentTransactionId { get; set; }
    
    public string TaskType { get; set; } = string.Empty; // AmountMismatch, LoanNotFound, DuplicatePayment
    public string Status { get; set; } = "Pending"; // Pending, InProgress, Resolved, Escalated
    
    public string Description { get; set; } = string.Empty;
    public decimal ExpectedAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public decimal Variance { get; set; }
    
    public string? AssignedTo { get; set; }
    public DateTime? AssignedAt { get; set; }
    
    public string? Resolution { get; set; }
    public string? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
    
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    
    // Navigation
    public PaymentTransaction PaymentTransaction { get; set; } = null!;
}
