namespace IntelliFin.FinancialService.Models;

public class EmployeeVerificationRequest
{
    public string EmployeeId { get; set; } = string.Empty;
    public string NationalId { get; set; } = string.Empty;
    public string Ministry { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}

public class EmployeeVerificationResult
{
    public bool IsVerified { get; set; }
    public string EmployeeId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Ministry { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public decimal MonthlySalary { get; set; }
    public decimal MaxDeductionAmount { get; set; }
    public bool IsEligibleForDeduction { get; set; }
    public string VerificationStatus { get; set; } = string.Empty;
    public List<string> ValidationErrors { get; set; } = new();
    public DateTime VerificationDate { get; set; }
}

public class DeductionSubmissionRequest
{
    public string CycleId { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public List<DeductionItem> Items { get; set; } = new();
    public DateTime SubmissionDate { get; set; }
    public string SubmittedBy { get; set; } = string.Empty;
}

public class DeductionItem
{
    public string EmployeeId { get; set; } = string.Empty;
    public string LoanId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public DeductionType Type { get; set; }
    public DeductionStatus Status { get; set; }
    public string ExternalReference { get; set; } = string.Empty;
}

public class DeductionSubmissionResult
{
    public bool Success { get; set; }
    public string SubmissionId { get; set; } = string.Empty;
    public string CycleId { get; set; } = string.Empty;
    public int TotalItems { get; set; }
    public int AcceptedItems { get; set; }
    public int RejectedItems { get; set; }
    public List<DeductionItemResult> ItemResults { get; set; } = new();
    public string Message { get; set; } = string.Empty;
    public DateTime SubmissionDate { get; set; }
}

public class DeductionItemResult
{
    public string EmployeeId { get; set; } = string.Empty;
    public string LoanId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string ExternalReference { get; set; } = string.Empty;
}

public class DeductionResultsResponse
{
    public string CycleId { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public DeductionCycleStatus Status { get; set; }
    public List<DeductionProcessingResult> Results { get; set; } = new();
    public DateTime ProcessingDate { get; set; }
    public decimal TotalProcessed { get; set; }
    public int ItemsProcessed { get; set; }
}

public class DeductionProcessingResult
{
    public string EmployeeId { get; set; } = string.Empty;
    public string LoanId { get; set; } = string.Empty;
    public decimal RequestedAmount { get; set; }
    public decimal ProcessedAmount { get; set; }
    public DeductionStatus Status { get; set; }
    public string StatusReason { get; set; } = string.Empty;
    public string ExternalReference { get; set; } = string.Empty;
    public DateTime ProcessedDate { get; set; }
}

public class DeductionStatusResult
{
    public string DeductionId { get; set; } = string.Empty;
    public DeductionStatus Status { get; set; }
    public string StatusDescription { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; }
    public string ExternalReference { get; set; } = string.Empty;
}

public class PmecHealthCheckResult
{
    public bool IsConnected { get; set; }
    public string Status { get; set; } = string.Empty;
    public TimeSpan ResponseTime { get; set; }
    public string Version { get; set; } = string.Empty;
    public DateTime LastChecked { get; set; }
    public List<string> Issues { get; set; } = new();
}

public enum DeductionType
{
    LoanRepayment,
    InterestPayment,
    PenaltyFee,
    ProcessingFee,
    Other
}

public enum DeductionStatus
{
    Pending,
    Submitted,
    Processing,
    Processed,
    Failed,
    Cancelled,
    Rejected
}
