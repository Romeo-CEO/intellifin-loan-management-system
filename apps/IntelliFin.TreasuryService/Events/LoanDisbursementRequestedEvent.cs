using System.ComponentModel.DataAnnotations;

namespace IntelliFin.TreasuryService.Events;

/// <summary>
/// Event published when a loan disbursement is requested from Loan Origination
/// </summary>
public class LoanDisbursementRequestedEvent
{
    /// <summary>
    /// Unique identifier for the disbursement request
    /// </summary>
    [Required]
    public Guid DisbursementId { get; set; }

    /// <summary>
    /// Loan unique identifier
    /// </summary>
    [Required]
    public string LoanId { get; set; } = string.Empty;

    /// <summary>
    /// Client unique identifier
    /// </summary>
    [Required]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Client's full name for display and audit
    /// </summary>
    [Required]
    public string ClientName { get; set; } = string.Empty;

    /// <summary>
    /// Disbursement amount in MWK
    /// </summary>
    [Required]
    public decimal Amount { get; set; }

    /// <summary>
    /// Currency code (always MWK for Malawi)
    /// </summary>
    [Required]
    public string Currency { get; set; } = "MWK";

    /// <summary>
    /// Client's bank account number for disbursement
    /// </summary>
    [Required]
    public string BankAccountNumber { get; set; } = string.Empty;

    /// <summary>
    /// Bank code for routing (e.g., FCB, NBM, etc.)
    /// </summary>
    [Required]
    public string BankCode { get; set; } = string.Empty;

    /// <summary>
    /// When the disbursement was requested
    /// </summary>
    [Required]
    public DateTime RequestedAt { get; set; }

    /// <summary>
    /// Who requested the disbursement (user ID)
    /// </summary>
    [Required]
    public string RequestedBy { get; set; } = string.Empty;

    /// <summary>
    /// Idempotency key to prevent duplicate processing
    /// </summary>
    [Required]
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>
    /// Correlation ID for tracking across services
    /// </summary>
    [Required]
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Camunda process instance ID if initiated from workflow
    /// </summary>
    public string? ProcessInstanceId { get; set; }

    /// <summary>
    /// Additional loan context information
    /// </summary>
    public LoanContext? LoanContext { get; set; }
}

/// <summary>
/// Additional context about the loan for disbursement processing
/// </summary>
public class LoanContext
{
    /// <summary>
    /// Loan product type (e.g., Personal, Business, Agricultural)
    /// </summary>
    public string LoanProduct { get; set; } = string.Empty;

    /// <summary>
    /// Loan term in months
    /// </summary>
    public int TermMonths { get; set; }

    /// <summary>
    /// Interest rate percentage
    /// </summary>
    public decimal InterestRate { get; set; }

    /// <summary>
    /// Risk rating of the loan
    /// </summary>
    public string RiskRating { get; set; } = string.Empty;

    /// <summary>
    /// Branch where loan was originated
    /// </summary>
    public string OriginatingBranch { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is a first-time disbursement or subsequent
    /// </summary>
    public bool IsFirstDisbursement { get; set; } = true;
}

