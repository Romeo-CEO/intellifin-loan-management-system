using System;

namespace IntelliFin.AdminService.Models;

public class RecertificationEscalation
{
    public long Id { get; set; }
    public Guid EscalationId { get; set; }
    public Guid TaskId { get; set; }
    public string CampaignId { get; set; } = string.Empty;
    public string OriginalManagerUserId { get; set; } = string.Empty;
    public string EscalatedToUserId { get; set; } = string.Empty;
    public string EscalationType { get; set; } = string.Empty;
    public DateTime EscalatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? Resolution { get; set; }
    public string? ResolutionComments { get; set; }
}
