namespace IntelliFin.Shared.DomainModels.Entities;

/// <summary>
/// Represents a loan application in the origination system.
/// Supports versioning for audit trails and compliance tracking.
/// </summary>
public class LoanApplication
{
    /// <summary>Unique identifier for the application</summary>
    public Guid Id { get; set; }
    
    /// <summary>Client ID who submitted the application</summary>
    public Guid ClientId { get; set; }
    
    /// <summary>Approved or current loan amount</summary>
    public decimal Amount { get; set; }
    
    /// <summary>Loan term in months</summary>
    public int TermMonths { get; set; }
    
    /// <summary>Product code (e.g., "PAYROLL", "BUSINESS", "ASSET")</summary>
    public string ProductCode { get; set; } = string.Empty;
    
    /// <summary>Product name for display</summary>
    public string ProductName { get; set; } = string.Empty;
    
    /// <summary>Current status (Draft, Submitted, PendingKYC, PendingCreditAssessment, PendingApproval, Approved, Rejected, Withdrawn)</summary>
    public string Status { get; set; } = "Draft";
    
    /// <summary>When the application was created</summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    
    /// <summary>When the application was submitted for processing</summary>
    public DateTime? SubmittedAt { get; set; }
    
    /// <summary>When the application was approved</summary>
    public DateTime? ApprovedAt { get; set; }
    
    /// <summary>User who approved the application</summary>
    public string? ApprovedBy { get; set; }
    
    /// <summary>JSON data for the application (flexible attributes)</summary>
    public string ApplicationDataJson { get; set; } = "{}";
    
    /// <summary>Originally requested loan amount</summary>
    public decimal RequestedAmount { get; set; }
    
    /// <summary>Camunda/Zeebe workflow instance ID for orchestration</summary>
    public string? WorkflowInstanceId { get; set; }
    
    /// <summary>Reason for decline if rejected</summary>
    public string? DeclineReason { get; set; }
    
    // ===== VERSIONING AND AUDIT FIELDS =====
    
    /// <summary>Unique loan number assigned on approval (e.g., "CHD-2025-00001")</summary>
    public string? LoanNumber { get; set; }
    
    /// <summary>Version number for audit trail (increments with each state change)</summary>
    public int Version { get; set; } = 1;
    
    /// <summary>Parent version ID for version history tracking</summary>
    public Guid? ParentVersionId { get; set; }
    
    /// <summary>Whether this is the current/active version of the loan</summary>
    public bool IsCurrentVersion { get; set; } = true;
    
    /// <summary>Risk grade (A=Excellent, B=Good, C=Fair, D=Poor, E=Very Poor, F=Unacceptable)</summary>
    public string? RiskGrade { get; set; }
    
    /// <summary>Effective Annual Rate (EAR) including all fees</summary>
    public decimal? EffectiveAnnualRate { get; set; }
    
    /// <summary>Agreement document hash for integrity verification (SHA256)</summary>
    public string? AgreementFileHash { get; set; }
    
    /// <summary>MinIO path where agreement PDF is stored</summary>
    public string? AgreementMinioPath { get; set; }
    
    /// <summary>When the agreement was generated (UTC)</summary>
    public DateTime? AgreementGeneratedAt { get; set; }
    
    /// <summary>User who created the application (for audit and dual control)</summary>
    public string? CreatedBy { get; set; }
    
    /// <summary>Last user who modified the application</summary>
    public string? LastModifiedBy { get; set; }
    
    /// <summary>When the application was last modified</summary>
    public DateTime? LastModifiedAtUtc { get; set; }

    // ===== NAVIGATION PROPERTIES =====
    
    public Client? Client { get; set; }
    public LoanProduct? Product { get; set; }
    public ICollection<CreditAssessment> CreditAssessments { get; set; } = new List<CreditAssessment>();
}

