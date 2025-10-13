namespace IntelliFin.AdminService.Models;

public class EmergencyAccessLog
{
    public long Id { get; set; }

    public Guid EmergencyId { get; set; } = Guid.NewGuid();

    public string RequestedBy { get; set; } = string.Empty;

    public string ApprovedBy1 { get; set; } = string.Empty;

    public string ApprovedBy2 { get; set; } = string.Empty;

    public string IncidentTicketId { get; set; } = string.Empty;

    public string Justification { get; set; } = string.Empty;

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddHours(1);

    public string? VaultOneTimeToken { get; set; }
        = string.Empty;

    public bool TokenUsed { get; set; }
        = false;

    public DateTime? TokenUsedAt { get; set; }
        = null;

    public bool PostIncidentReviewCompleted { get; set; }
        = false;

    public DateTime? ReviewCompletedAt { get; set; }
        = null;

    public string? ReviewNotes { get; set; } = string.Empty;
}
