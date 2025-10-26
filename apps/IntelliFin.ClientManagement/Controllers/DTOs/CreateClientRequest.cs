namespace IntelliFin.ClientManagement.Controllers.DTOs;

/// <summary>
/// Request DTO for creating a new client
/// </summary>
public class CreateClientRequest
{
    /// <summary>
    /// National Registration Card number (format: XXXXXX/XX/X)
    /// </summary>
    public string Nrc { get; set; } = string.Empty;

    /// <summary>
    /// Government payroll number (optional, for PMEC integration)
    /// </summary>
    public string? PayrollNumber { get; set; }

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
    /// Date of birth (must be at least 18 years old)
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

    /// <summary>
    /// Branch ID where client is being created
    /// </summary>
    public Guid BranchId { get; set; }
}
