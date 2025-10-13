using System;

namespace IntelliFin.AdminService.Models;

public class RecertificationTask
{
    public long Id { get; set; }
    public Guid TaskId { get; set; }
    public string CampaignId { get; set; } = string.Empty;
    public string ManagerUserId { get; set; } = string.Empty;
    public string ManagerName { get; set; } = string.Empty;
    public string ManagerEmail { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public int UsersInScope { get; set; }
    public int UsersReviewed { get; set; }
    public int RemindersSent { get; set; }
    public DateTime? LastReminderAt { get; set; }
    public string? EscalatedTo { get; set; }
    public DateTime? EscalatedAt { get; set; }
    public string? CamundaTaskId { get; set; }
}
