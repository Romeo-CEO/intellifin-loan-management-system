namespace IntelliFin.ClientManagement.Domain.Entities;

/// <summary>
/// Represents a client/customer in the system
/// </summary>
public class Client
{
    /// <summary>
    /// Unique identifier for the client
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// National Registration Card number (11 characters, format: XXXXXX/XX/X)
    /// </summary>
    public string Nrc { get; set; } = string.Empty;

    /// <summary>
    /// Government payroll number (reserved for PMEC integration)
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
    /// Client's date of birth (must be at least 18 years old)
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
    /// Nationality (default: Zambian)
    /// </summary>
    public string? Nationality { get; set; }

    // Employment Information (Reserved for PMEC)

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
    /// Primary phone number (Zambian format: +260...)
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
    /// KYC verification status (Pending, Approved, EDD_Required, Rejected)
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
    public bool IsPep { get; set; } = false;

    /// <summary>
    /// Indicates if client is on sanctions list
    /// </summary>
    public bool IsSanctioned { get; set; } = false;

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

    /// <summary>
    /// Date and time when client was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User who created the client
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// Date and time when client was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User who last updated the client
    /// </summary>
    public string UpdatedBy { get; set; } = string.Empty;

    /// <summary>
    /// Version number (always 1 for now, versioning in Story 1.4)
    /// </summary>
    public int VersionNumber { get; set; } = 1;

    // Navigation properties
    
    /// <summary>
    /// Collection of documents uploaded for this client (Story 1.6)
    /// </summary>
    public ICollection<ClientDocument> Documents { get; set; } = new List<ClientDocument>();

    /// <summary>
    /// Collection of communication consent preferences for this client (Story 1.7)
    /// </summary>
    public ICollection<CommunicationConsent> Consents { get; set; } = new List<CommunicationConsent>();
}
