namespace IntelliFin.AdminService.Models;

public class IncidentPlaybookRun
{
    public int Id { get; set; }
    public Guid RunId { get; set; } = Guid.NewGuid();
    public int IncidentPlaybookId { get; set; }
    public IncidentPlaybook? Playbook { get; set; }
    public Guid IncidentId { get; set; }
    public string AlertName { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AcknowledgedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public double? ResolutionMinutes { get; set; }
    public bool AutomationInvoked { get; set; }
    public string? AutomationOutcome { get; set; }
    public string? ResolutionSummary { get; set; }
    public string? RecordedBy { get; set; }
    public string? CamundaProcessInstanceId { get; set; }
    public string? PagerDutyIncidentId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
