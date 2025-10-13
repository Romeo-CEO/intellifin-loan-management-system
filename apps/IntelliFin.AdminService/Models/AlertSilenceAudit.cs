namespace IntelliFin.AdminService.Models;

public class AlertSilenceAudit
{
    public int Id { get; set; }
    public Guid AuditId { get; set; } = Guid.NewGuid();
    public string SilenceId { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public string Matchers { get; set; } = string.Empty;
    public string AlertmanagerUrl { get; set; } = string.Empty;
}
