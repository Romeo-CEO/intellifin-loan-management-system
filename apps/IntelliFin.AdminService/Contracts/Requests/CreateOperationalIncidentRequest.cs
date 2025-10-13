using System.ComponentModel.DataAnnotations;

namespace IntelliFin.AdminService.Contracts.Requests;

public sealed class CreateOperationalIncidentRequest
{
    [Required]
    [StringLength(150)]
    public string AlertName { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string Severity { get; set; } = "critical";

    [StringLength(500)]
    public string? Summary { get; set; }

    public string? Details { get; set; }

    public Guid? PlaybookId { get; set; }

    [StringLength(100)]
    public string? PagerDutyIncidentId { get; set; }

    [StringLength(300)]
    public string? SlackThreadUrl { get; set; }

    public DateTime? DetectedAt { get; set; }
}
