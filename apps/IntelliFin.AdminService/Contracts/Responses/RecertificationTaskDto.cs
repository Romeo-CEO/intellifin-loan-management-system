using System;

namespace IntelliFin.AdminService.Contracts.Responses;

public class RecertificationTaskDto
{
    public Guid TaskId { get; set; }
    public string CampaignId { get; set; } = string.Empty;
    public string CampaignName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public int UsersInScope { get; set; }
    public int UsersReviewed { get; set; }
    public int RemindersSent { get; set; }
    public DateTime? LastReminderAt { get; set; }
    public string? EscalatedTo { get; set; }
    public DateTime? EscalatedAt { get; set; }
}
