namespace IntelliFin.Collections.Infrastructure.Messaging.Contracts;

/// <summary>
/// Event published when a treasury payment (bank transfer, cash) is received.
/// </summary>
public record TreasuryPaymentReceived
{
    public Guid LoanId { get; init; }
    public Guid ClientId { get; init; }
    public string TransactionReference { get; init; } = string.Empty;
    public string PaymentMethod { get; init; } = string.Empty; // BankTransfer, Cash, MobileMoney
    public decimal Amount { get; init; }
    public DateTime TransactionDate { get; init; }
    public string? BankReference { get; init; }
    public string? Notes { get; init; }
    public string ReceivedBy { get; init; } = string.Empty;
    
    // Event metadata
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime EventTimestamp { get; init; } = DateTime.UtcNow;
    public string EventType => "TreasuryPaymentReceived";
    public string SourceService => "FinancialService";
    public string? CorrelationId { get; init; }
}
