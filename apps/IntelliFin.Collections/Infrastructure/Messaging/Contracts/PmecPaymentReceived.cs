namespace IntelliFin.Collections.Infrastructure.Messaging.Contracts;

/// <summary>
/// Event published when a PMEC payroll deduction payment is received.
/// </summary>
public record PmecPaymentReceived
{
    public Guid LoanId { get; init; }
    public Guid ClientId { get; init; }
    public string PmecReference { get; init; } = string.Empty;
    public string EmployeeNumber { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public DateTime PayrollPeriod { get; init; }
    public DateTime DeductionDate { get; init; }
    public string? Notes { get; init; }
    
    // Event metadata
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime EventTimestamp { get; init; } = DateTime.UtcNow;
    public string EventType => "PmecPaymentReceived";
    public string SourceService => "FinancialService";
    public string? CorrelationId { get; init; }
}
