using System;

namespace IntelliFin.AdminService.Contracts.Responses;

public class RecertificationReportDto
{
    public Guid ReportId { get; set; }
    public string CampaignId { get; set; } = string.Empty;
    public string ReportType { get; set; } = string.Empty;
    public string ReportFormat { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public string? FilePath { get; set; }
}
