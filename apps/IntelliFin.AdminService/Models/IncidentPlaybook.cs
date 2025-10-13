namespace IntelliFin.AdminService.Models;

public class IncidentPlaybook
{
    public int Id { get; set; }
    public Guid PlaybookId { get; set; } = Guid.NewGuid();
    public string AlertName { get; set; } = string.Empty;
    public string Severity { get; set; } = "critical";
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string DiagnosisSteps { get; set; } = string.Empty;
    public string ResolutionSteps { get; set; } = string.Empty;
    public string EscalationPath { get; set; } = string.Empty;
    public string? LinkedRunbookUrl { get; set; }
    public string Owner { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string? AutomationProcessKey { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }
    public ICollection<IncidentPlaybookRun> Runs { get; set; } = new List<IncidentPlaybookRun>();
    public ICollection<OperationalIncident> Incidents { get; set; } = new List<OperationalIncident>();
}
