using System;
using System.Collections.Generic;

namespace IntelliFin.AdminService.Contracts.Responses;

public class RecertificationUserReviewDto
{
    public Guid ReviewId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string? Department { get; set; }
    public string? JobTitle { get; set; }
    public IReadOnlyList<string> CurrentRoles { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> CurrentPermissions { get; set; } = Array.Empty<string>();
    public DateTime? LastLoginDate { get; set; }
    public DateTime? AccessGrantedDate { get; set; }
    public string? RiskLevel { get; set; }
    public IReadOnlyList<string> RiskIndicators { get; set; } = Array.Empty<string>();
    public string? Decision { get; set; }
    public string? DecisionComments { get; set; }
    public DateTime? DecisionMadeAt { get; set; }
}
