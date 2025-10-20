namespace IntelliFin.ClientManagement.Domain.Entities;

/// <summary>
/// Represents a temporal version snapshot of a client using SCD-2 (Slowly Changing Dimension Type 2) pattern.
/// Each update to a client creates a new version record with full snapshot of client state.
/// </summary>
public class ClientVersion
{
    /// <summary>
    /// Unique identifier for this version record
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Foreign key to the Client this version belongs to
    /// </summary>
    public Guid ClientId { get; set; }

    /// <summary>
    /// Sequential version number (1, 2, 3...)
    /// </summary>
    public int VersionNumber { get; set; }

    // Full snapshot of client data at this version
    // These fields mirror the Client entity to store complete state

    /// <summary>
    /// National Registration Card number
    /// </summary>
    public string Nrc { get; set; } = string.Empty;

    /// <summary>
    /// Government payroll number
    /// </summary>
    public string? PayrollNumber { get; set; }

    // Personal Information

    /// <summary>
    /// Client's first name
    /// </summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// Client's last name
    /// </summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Client's other names (middle names)
    /// </summary>
    public string? OtherNames { get; set; }

    /// <summary>
    /// Client's date of birth
    /// </summary>
    public DateTime DateOfBirth { get; set; }

    /// <summary>
    /// Gender (M, F, Other)
    /// </summary>
    public string Gender { get; set; } = string.Empty;

    /// <summary>
    /// Marital status
    /// </summary>
    public string MaritalStatus { get; set; } = string.Empty;

    /// <summary>
    /// Nationality
    /// </summary>
    public string? Nationality { get; set; }

    // Employment Information

    /// <summary>
    /// Government ministry (for government employees)
    /// </summary>
    public string? Ministry { get; set; }

    /// <summary>
    /// Employer type (Government, Private, Self)
    /// </summary>
    public string? EmployerType { get; set; }

    /// <summary>
    /// Employment status (Active, Suspended, Terminated)
    /// </summary>
    public string? EmploymentStatus { get; set; }

    // Contact Information

    /// <summary>
    /// Primary phone number
    /// </summary>
    public string PrimaryPhone { get; set; } = string.Empty;

    /// <summary>
    /// Secondary phone number
    /// </summary>
    public string? SecondaryPhone { get; set; }

    /// <summary>
    /// Email address
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Physical address
    /// </summary>
    public string PhysicalAddress { get; set; } = string.Empty;

    /// <summary>
    /// City
    /// </summary>
    public string City { get; set; } = string.Empty;

    /// <summary>
    /// Province
    /// </summary>
    public string Province { get; set; } = string.Empty;

    // Compliance Information

    /// <summary>
    /// KYC verification status
    /// </summary>
    public string KycStatus { get; set; } = "Pending";

    /// <summary>
    /// Date and time when KYC was completed
    /// </summary>
    public DateTime? KycCompletedAt { get; set; }

    /// <summary>
    /// User who completed KYC verification
    /// </summary>
    public string? KycCompletedBy { get; set; }

    /// <summary>
    /// AML risk level (Low, Medium, High)
    /// </summary>
    public string AmlRiskLevel { get; set; } = "Low";

    /// <summary>
    /// Indicates if client is a Politically Exposed Person
    /// </summary>
    public bool IsPep { get; set; }

    /// <summary>
    /// Indicates if client is on sanctions list
    /// </summary>
    public bool IsSanctioned { get; set; }

    // Risk Information

    /// <summary>
    /// Overall risk rating (Low, Medium, High)
    /// </summary>
    public string RiskRating { get; set; } = "Low";

    /// <summary>
    /// Date and time of last risk assessment
    /// </summary>
    public DateTime? RiskLastAssessedAt { get; set; }

    // Lifecycle Information

    /// <summary>
    /// Client status (Active, Inactive, Archived)
    /// </summary>
    public string Status { get; set; } = "Active";

    /// <summary>
    /// Branch where client was created
    /// </summary>
    public Guid BranchId { get; set; }

    // Temporal Tracking (SCD-2)

    /// <summary>
    /// Start timestamp when this version became valid
    /// </summary>
    public DateTime ValidFrom { get; set; }

    /// <summary>
    /// End timestamp when this version stopped being valid (null for current version)
    /// </summary>
    public DateTime? ValidTo { get; set; }

    /// <summary>
    /// Indicates if this is the current/active version (only one version per client can be current)
    /// </summary>
    public bool IsCurrent { get; set; }

    // Change Tracking

    /// <summary>
    /// JSON summary of what changed in this version (e.g., {"fields": ["Phone", "Address"], "changes": [...]})
    /// </summary>
    public string ChangeSummaryJson { get; set; } = string.Empty;

    /// <summary>
    /// Free text reason for the change
    /// </summary>
    public string ChangeReason { get; set; } = string.Empty;

    /// <summary>
    /// Date and time when this version was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// User who created this version
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// IP address of the user who made the change
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// Correlation ID for audit trail
    /// </summary>
    public string? CorrelationId { get; set; }

    // Navigation Properties

    /// <summary>
    /// Navigation property to the Client entity
    /// </summary>
    public Client? Client { get; set; }
}
