using System.ComponentModel.DataAnnotations;

namespace IntelliFin.AdminService.Contracts.Requests;

public sealed class RecordPlaybookUsageRequest
{
    [Required]
    public Guid IncidentId { get; set; }

    [Required]
    [StringLength(150)]
    public string AlertName { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string Severity { get; set; } = "critical";

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? AcknowledgedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    [StringLength(2000)]
    public string? ResolutionSummary { get; set; }

    public bool AutomationInvoked { get; set; }

    [StringLength(200)]
    public string? AutomationOutcome { get; set; }

    [StringLength(100)]
    public string? PagerDutyIncidentId { get; set; }

    public double? ResolutionMinutesOverride { get; set; }
}
