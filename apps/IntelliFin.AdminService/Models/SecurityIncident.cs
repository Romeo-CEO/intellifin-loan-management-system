namespace IntelliFin.AdminService.Models;

public class SecurityIncident
{
    public int Id { get; set; }
    public Guid IncidentId { get; set; }
    public string IncidentType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? AffectedEntityType { get; set; }
    public string? AffectedEntityId { get; set; }
    public string ResolutionStatus { get; set; } = "OPEN";
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }
}
