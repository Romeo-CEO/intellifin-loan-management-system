using IntelliFin.ClientManagement.Domain.Enums;

namespace IntelliFin.ClientManagement.Domain.Entities;

/// <summary>
/// Tracks KYC (Know Your Customer) compliance state and progress for a client
/// One-to-one relationship with Client entity
/// Manages document completeness, AML screening, and EDD requirements
/// </summary>
public class KycStatus
{
    /// <summary>
    /// Unique identifier for the KYC status record
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Foreign key to Client entity (unique - one KYC status per client)
    /// </summary>
    public Guid ClientId { get; set; }

    // ========== State Management ==========

    /// <summary>
    /// Current KYC compliance state
    /// Valid values: Pending, InProgress, Completed, EDD_Required, Rejected
    /// </summary>
    public KycState CurrentState { get; set; } = KycState.Pending;

    /// <summary>
    /// When KYC process was initiated
    /// </summary>
    public DateTime? KycStartedAt { get; set; }

    /// <summary>
    /// When KYC was completed (state reached Completed)
    /// </summary>
    public DateTime? KycCompletedAt { get; set; }

    /// <summary>
    /// User ID who completed the KYC process
    /// </summary>
    public string? KycCompletedBy { get; set; }

    /// <summary>
    /// Camunda workflow process instance ID for tracking
    /// Links KYC status to BPMN workflow execution
    /// </summary>
    public string? CamundaProcessInstanceId { get; set; }

    // ========== Document Completeness Flags ==========

    /// <summary>
    /// Whether client has uploaded National Registration Card (NRC)
    /// Required for identity verification
    /// </summary>
    public bool HasNrc { get; set; }

    /// <summary>
    /// Whether client has uploaded proof of address
    /// Examples: Utility bill, bank statement
    /// </summary>
    public bool HasProofOfAddress { get; set; }

    /// <summary>
    /// Whether client has uploaded recent payslip
    /// For salaried employment verification
    /// </summary>
    public bool HasPayslip { get; set; }

    /// <summary>
    /// Whether client has uploaded employment letter
    /// Alternative to payslip for employment verification
    /// </summary>
    public bool HasEmploymentLetter { get; set; }

    /// <summary>
    /// Computed property: Are all required documents uploaded?
    /// True if: HasNrc AND HasProofOfAddress AND (HasPayslip OR HasEmploymentLetter)
    /// NOTE: This will be a computed column in SQL Server
    /// </summary>
    public bool IsDocumentComplete
    {
        get => HasNrc && HasProofOfAddress && (HasPayslip || HasEmploymentLetter);
    }

    // ========== AML (Anti-Money Laundering) Screening ==========

    /// <summary>
    /// Whether AML screening has been completed
    /// Checks sanctions lists, PEP (Politically Exposed Person) status
    /// </summary>
    public bool AmlScreeningComplete { get; set; }

    /// <summary>
    /// When AML screening was performed
    /// </summary>
    public DateTime? AmlScreenedAt { get; set; }

    /// <summary>
    /// User ID who performed AML screening
    /// </summary>
    public string? AmlScreenedBy { get; set; }

    // ========== EDD (Enhanced Due Diligence) ==========

    /// <summary>
    /// Whether this client requires Enhanced Due Diligence
    /// Triggered by: PEP status, sanctions, high risk, tampered documents
    /// </summary>
    public bool RequiresEdd { get; set; }

    /// <summary>
    /// Reason EDD was required
    /// Values: "PEP", "Sanctions", "HighRisk", "TamperedDoc"
    /// </summary>
    public string? EddReason { get; set; }

    /// <summary>
    /// When EDD process was escalated
    /// </summary>
    public DateTime? EddEscalatedAt { get; set; }

    /// <summary>
    /// MinIO object key for EDD report PDF
    /// Format: edd-reports/{clientId}/report-{timestamp}.pdf
    /// </summary>
    public string? EddReportObjectKey { get; set; }

    /// <summary>
    /// Compliance officer who approved EDD
    /// First level of approval required
    /// </summary>
    public string? EddApprovedBy { get; set; }

    /// <summary>
    /// CEO who approved EDD
    /// Second level of approval required for high-risk clients
    /// </summary>
    public string? EddCeoApprovedBy { get; set; }

    /// <summary>
    /// When EDD was approved (both compliance and CEO)
    /// </summary>
    public DateTime? EddApprovedAt { get; set; }

    /// <summary>
    /// Risk acceptance level for EDD-approved clients
    /// Values: Standard, EnhancedMonitoring, RestrictedServices
    /// </summary>
    public string? RiskAcceptanceLevel { get; set; }

    /// <summary>
    /// Compliance officer comments/notes from EDD review
    /// </summary>
    public string? ComplianceComments { get; set; }

    /// <summary>
    /// CEO comments/rationale from EDD approval
    /// </summary>
    public string? CeoComments { get; set; }

    // ========== Audit Fields ==========

    /// <summary>
    /// When this KYC status record was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this KYC status record was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // ========== Navigation Properties ==========

    /// <summary>
    /// Navigation property to associated Client
    /// </summary>
    public Client? Client { get; set; }
}
