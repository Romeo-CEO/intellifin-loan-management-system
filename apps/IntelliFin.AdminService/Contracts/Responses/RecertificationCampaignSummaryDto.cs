using System;

namespace IntelliFin.AdminService.Contracts.Responses;

public class RecertificationCampaignSummaryDto
{
    public string CampaignId { get; set; } = string.Empty;
    public string CampaignName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime DueDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public int TotalUsersInScope { get; set; }
    public int UsersReviewed { get; set; }
    public int UsersApproved { get; set; }
    public int UsersRevoked { get; set; }
    public int UsersModified { get; set; }
    public decimal CompletionPercentage { get; set; }
    public int EscalationCount { get; set; }
}
