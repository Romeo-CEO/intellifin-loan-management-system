using System;

namespace IntelliFin.AdminService.Contracts.Responses;

public class CampaignStatisticsDto
{
    public string CampaignId { get; set; } = string.Empty;
    public int TotalUsersInScope { get; set; }
    public int UsersReviewed { get; set; }
    public int UsersApproved { get; set; }
    public int UsersRevoked { get; set; }
    public int UsersModified { get; set; }
    public int ManagerTaskCount { get; set; }
    public int CompletedTaskCount { get; set; }
    public int OverdueTaskCount { get; set; }
    public int EscalationCount { get; set; }
    public decimal CompletionPercentage { get; set; }
}
