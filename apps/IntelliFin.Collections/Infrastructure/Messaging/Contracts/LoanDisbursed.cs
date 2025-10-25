namespace IntelliFin.Collections.Infrastructure.Messaging.Contracts;

/// <summary>
/// Event published when a loan is successfully disbursed.
/// Consumed by Collections service to generate repayment schedule.
/// </summary>
public record LoanDisbursed
{
    public Guid LoanId { get; init; }
    public Guid ClientId { get; init; }
    public string ProductCode { get; init; } = string.Empty;
    public decimal DisbursedAmount { get; init; }
    public decimal InterestRate { get; init; }
    public int TermMonths { get; init; }
    public DateTime DisbursementDate { get; init; }
    public DateTime FirstPaymentDate { get; init; }
    public string DisbursedBy { get; init; } = string.Empty;
    public string? WorkflowInstanceId { get; init; }
    
    // Event metadata
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime EventTimestamp { get; init; } = DateTime.UtcNow;
    public string EventType => "LoanDisbursed";
    public string SourceService => "FinancialService";
    public string? CorrelationId { get; init; }
}
