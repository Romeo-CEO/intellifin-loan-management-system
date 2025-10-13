namespace IntelliFin.AdminService.Models;

public class OperationalIncident
{
    public int Id { get; set; }
    public Guid IncidentId { get; set; } = Guid.NewGuid();
    public string AlertName { get; set; } = string.Empty;
    public string Severity { get; set; } = "critical";
    public string Status { get; set; } = "Open";
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AcknowledgedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? Summary { get; set; }
    public string? Details { get; set; }
    public string? PagerDutyIncidentId { get; set; }
    public string? SlackThreadUrl { get; set; }
    public string? CamundaProcessInstanceId { get; set; }
    public int? IncidentPlaybookId { get; set; }
    public IncidentPlaybook? Playbook { get; set; }
    public string? CreatedBy { get; set; }
    public string? LastUpdatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PostmortemDueAt { get; set; }
    public DateTime? PostmortemCompletedAt { get; set; }
    public string? PostmortemSummary { get; set; }
    public string? AutomationStatus { get; set; }
}
