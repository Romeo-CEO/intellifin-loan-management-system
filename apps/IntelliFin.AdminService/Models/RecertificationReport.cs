using System;

namespace IntelliFin.AdminService.Models;

public class RecertificationReport
{
    public int Id { get; set; }
    public Guid ReportId { get; set; }
    public string CampaignId { get; set; } = string.Empty;
    public string ReportType { get; set; } = string.Empty;
    public string ReportFormat { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public long FileSize { get; set; }
    public DateTime GeneratedAt { get; set; }
    public string? GeneratedBy { get; set; }
    public int AccessedCount { get; set; }
    public DateTime? LastAccessedAt { get; set; }
    public string? LastAccessedBy { get; set; }
    public DateTime RetentionDate { get; set; }
}
