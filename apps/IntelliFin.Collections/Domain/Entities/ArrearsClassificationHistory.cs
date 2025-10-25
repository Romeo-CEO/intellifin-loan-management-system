namespace IntelliFin.Collections.Domain.Entities;

/// <summary>
/// Tracks the history of BoZ arrears classifications for loans.
/// Maintains immutable audit trail per regulatory requirements.
/// </summary>
public class ArrearsClassificationHistory
{
    public Guid Id { get; set; }
    public Guid LoanId { get; set; }
    
    public string PreviousClassification { get; set; } = "Current";
    public string NewClassification { get; set; } = string.Empty; // Current, SpecialMention, Substandard, Doubtful, Loss
    
    public int DaysPastDue { get; set; }
    public decimal OutstandingBalance { get; set; }
    public decimal ProvisionRate { get; set; }
    public decimal ProvisionAmount { get; set; }
    
    public bool IsNonAccrual { get; set; }
    
    public DateTime ClassifiedAt { get; set; }
    public string ClassifiedBy { get; set; } = string.Empty;
    
    public string? Reason { get; set; }
    public string? CorrelationId { get; set; }
    
    public DateTime CreatedAtUtc { get; set; }
}
