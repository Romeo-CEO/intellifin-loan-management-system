using System;

namespace IntelliFin.AdminService.Models;

public class RecertificationCampaign
{
    public int Id { get; set; }
    public string CampaignId { get; set; } = string.Empty;
    public string CampaignName { get; set; } = string.Empty;
    public int Quarter { get; set; }
    public int Year { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime? CompletionDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public int TotalUsersInScope { get; set; }
    public int UsersReviewed { get; set; }
    public int UsersApproved { get; set; }
    public int UsersRevoked { get; set; }
    public int UsersModified { get; set; }
    public int EscalationCount { get; set; }
    public string? CamundaProcessInstanceId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? CompletedBy { get; set; }
}
