namespace IntelliFin.ClientManagement.Controllers.DTOs;

/// <summary>
/// Response DTO for client information
/// </summary>
public class ClientResponse
{
    public Guid Id { get; set; }
    public string Nrc { get; set; } = string.Empty;
    public string? PayrollNumber { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? OtherNames { get; set; }
    public DateTime DateOfBirth { get; set; }
    public string Gender { get; set; } = string.Empty;
    public string MaritalStatus { get; set; } = string.Empty;
    public string? Nationality { get; set; }
    public string? Ministry { get; set; }
    public string? EmployerType { get; set; }
    public string? EmploymentStatus { get; set; }
    public string PrimaryPhone { get; set; } = string.Empty;
    public string? SecondaryPhone { get; set; }
    public string? Email { get; set; }
    public string PhysicalAddress { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public string KycStatus { get; set; } = string.Empty;
    public DateTime? KycCompletedAt { get; set; }
    public string? KycCompletedBy { get; set; }
    public string AmlRiskLevel { get; set; } = string.Empty;
    public bool IsPep { get; set; }
    public bool IsSanctioned { get; set; }
    public string RiskRating { get; set; } = string.Empty;
    public DateTime? RiskLastAssessedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid BranchId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
    public string UpdatedBy { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
}
