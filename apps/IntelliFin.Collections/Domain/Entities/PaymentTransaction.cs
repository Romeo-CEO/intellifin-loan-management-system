namespace IntelliFin.Collections.Domain.Entities;

/// <summary>
/// Represents a payment transaction applied to loan installments.
/// </summary>
public class PaymentTransaction
{
    public Guid Id { get; set; }
    public Guid LoanId { get; set; }
    public Guid ClientId { get; set; }
    public Guid? InstallmentId { get; set; }
    
    public string TransactionReference { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty; // PMEC, Manual, Treasury, Bank
    public string PaymentSource { get; set; } = string.Empty; // Payroll, BankTransfer, Cash, MobileMoney
    
    public decimal Amount { get; set; }
    public decimal PrincipalPortion { get; set; }
    public decimal InterestPortion { get; set; }
    public decimal PenaltyPortion { get; set; }
    
    public DateTime TransactionDate { get; set; }
    public DateTime ReceivedDate { get; set; }
    
    public string Status { get; set; } = "Pending"; // Pending, Confirmed, Reconciled, Reversed
    public bool IsReconciled { get; set; }
    public DateTime? ReconciledAt { get; set; }
    public string? ReconciledBy { get; set; }
    
    public string? ExternalReference { get; set; }
    public string? Notes { get; set; }
    
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    
    // Audit
    public string? CorrelationId { get; set; }
    
    // Navigation
    public Installment? Installment { get; set; }
}
