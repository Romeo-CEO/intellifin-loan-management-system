using System.ComponentModel.DataAnnotations;

namespace IntelliFin.AdminService.Contracts.Requests;

public class VaultLeaseRevocationRequest
{
    [Required]
    [MaxLength(200)]
    public string LeaseId { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Reason { get; set; }
        = "Emergency rotation initiated by security";

    [MaxLength(100)]
    public string? IncidentId { get; set; }
        = string.Empty;
}
