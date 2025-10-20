namespace IntelliFin.ClientManagement.Controllers.DTOs;

/// <summary>
/// Request DTO for updating an existing client
/// </summary>
public class UpdateClientRequest
{
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
    /// Marital status
    /// </summary>
    public string MaritalStatus { get; set; } = string.Empty;

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

    /// <summary>
    /// Employment information (for PMEC updates)
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
}
