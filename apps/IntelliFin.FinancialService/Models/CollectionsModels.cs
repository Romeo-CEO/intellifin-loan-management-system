namespace IntelliFin.FinancialService.Models;

public class CollectionsAccount
{
    public string LoanId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public decimal PrincipalBalance { get; set; }
    public decimal InterestBalance { get; set; }
    public decimal FeesBalance { get; set; }
    public decimal TotalBalance { get; set; }
    public DateTime LastPaymentDate { get; set; }
    public decimal LastPaymentAmount { get; set; }
    public int DaysPastDue { get; set; }
    public CollectionsStatus Status { get; set; }
    public BoZClassification BoZClassification { get; set; }
    public decimal ProvisionAmount { get; set; }
    public DateTime NextDueDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class DPDCalculationResult
{
    public string LoanId { get; set; } = string.Empty;
    public int DaysPastDue { get; set; }
    public DateTime CalculationDate { get; set; }
    public DateTime LastDueDate { get; set; }
    public decimal AmountOverdue { get; set; }
    public string CalculationMethod { get; set; } = string.Empty;
}

public class BoZClassificationResult
{
    public string LoanId { get; set; } = string.Empty;
    public BoZClassification Classification { get; set; }
    public decimal ProvisionRate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime ClassificationDate { get; set; }
}

public class ProvisioningResult
{
    public string LoanId { get; set; } = string.Empty;
    public decimal ProvisionAmount { get; set; }
    public decimal ProvisionRate { get; set; }
    public BoZClassification Classification { get; set; }
    public DateTime CalculationDate { get; set; }
}

public class CreateDeductionCycleRequest
{
    public string Period { get; set; } = string.Empty; // e.g., "2024-01"
    public List<string> LoanIds { get; set; } = new();
    public DateTime ProcessingDate { get; set; }
}

public class DeductionCycleResult
{
    public string CycleId { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public int TotalItems { get; set; }
    public int ProcessedItems { get; set; }
    public decimal TotalAmount { get; set; }
    public DeductionCycleStatus Status { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class RecordPaymentRequest
{
    public string LoanId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public string ExternalReference { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public class PaymentResult
{
    public bool Success { get; set; }
    public string PaymentId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public List<string> Errors { get; set; } = new();
}

public class CollectionsReport
{
    public DateTime ReportDate { get; set; }
    public int TotalAccounts { get; set; }
    public decimal TotalOutstanding { get; set; }
    public Dictionary<BoZClassification, int> ClassificationBreakdown { get; set; } = new();
    public Dictionary<CollectionsStatus, int> StatusBreakdown { get; set; } = new();
    public decimal TotalProvisions { get; set; }
}

public enum CollectionsStatus
{
    Current,
    EarlyDelinquency,
    Delinquent,
    Default,
    WriteOff,
    Recovered
}

public enum BoZClassification
{
    Normal,
    SpecialMention,
    Substandard,
    Doubtful,
    Loss
}

public enum DeductionCycleStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    PartiallyCompleted
}

public enum PaymentMethod
{
    Cash,
    BankTransfer,
    MobileMoney,
    Cheque,
    PayrollDeduction,
    Other
}
