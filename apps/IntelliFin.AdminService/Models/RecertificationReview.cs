using System;

namespace IntelliFin.AdminService.Models;

public class RecertificationReview
{
    public long Id { get; set; }
    public Guid ReviewId { get; set; }
    public Guid TaskId { get; set; }
    public string CampaignId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string? UserDepartment { get; set; }
    public string? UserJobTitle { get; set; }
    public string? CurrentRoles { get; set; }
    public string? CurrentPermissions { get; set; }
    public DateTime? LastLoginDate { get; set; }
    public DateTime? AccessGrantedDate { get; set; }
    public string? RiskLevel { get; set; }
    public string? RiskIndicators { get; set; }
    public string Decision { get; set; } = "Pending";
    public string? DecisionComments { get; set; }
    public string? DecisionMadeBy { get; set; }
    public DateTime? DecisionMadeAt { get; set; }
    public string? RolesToRevoke { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public int AppealsSubmitted { get; set; }
    public string? AppealStatus { get; set; }
}
