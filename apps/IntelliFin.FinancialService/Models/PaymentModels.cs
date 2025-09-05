namespace IntelliFin.FinancialService.Models;

public class Payment
{
    public string Id { get; set; } = string.Empty;
    public string LoanId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public string ExternalReference { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; }
    public DateTime PostedAt { get; set; }
    public DateTime? ReconciledAt { get; set; }
    public PaymentStatus Status { get; set; }
    public string ProcessorReference { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}

public class ProcessPaymentRequest
{
    public string LoanId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string? PhoneNumber { get; set; }
    public string? BankAccount { get; set; }
    public string? Reference { get; set; }
    public Dictionary<string, string> AdditionalData { get; set; } = new();
}

public class PaymentProcessingResult
{
    public bool Success { get; set; }
    public string PaymentId { get; set; } = string.Empty;
    public string TransactionId { get; set; } = string.Empty;
    public PaymentStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public decimal ProcessedAmount { get; set; }
    public DateTime ProcessedAt { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class PaymentReconciliationResult
{
    public string PaymentId { get; set; } = string.Empty;
    public bool IsReconciled { get; set; }
    public decimal ReconciledAmount { get; set; }
    public DateTime? ReconciliationDate { get; set; }
    public string ReconciliationReference { get; set; } = string.Empty;
    public List<string> Discrepancies { get; set; } = new();
}

public class PaymentStatusResult
{
    public string PaymentId { get; set; } = string.Empty;
    public PaymentStatus Status { get; set; }
    public string StatusDescription { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; }
    public string ExternalReference { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class ProcessRefundRequest
{
    public string PaymentId { get; set; } = string.Empty;
    public decimal RefundAmount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = string.Empty;
}

public class RefundResult
{
    public bool Success { get; set; }
    public string RefundId { get; set; } = string.Empty;
    public decimal RefundAmount { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class TinggPaymentRequest
{
    public string MerchantTransactionId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "ZMW";
    public string PhoneNumber { get; set; } = string.Empty;
    public string ServiceCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, string> AdditionalData { get; set; } = new();
}

public class TinggPaymentResult
{
    public bool Success { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public string TinggReference { get; set; } = string.Empty;
    public PaymentStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class PaymentGatewayHealthResult
{
    public string Gateway { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public TimeSpan ResponseTime { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime LastChecked { get; set; }
    public List<string> Issues { get; set; } = new();
}

public enum PaymentStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    Cancelled,
    Refunded,
    PartiallyRefunded
}
