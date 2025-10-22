namespace IntelliFin.ClientManagement.Controllers.DTOs;

/// <summary>
/// Response DTO for risk profile
/// </summary>
public class RiskProfileResponse
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public string RiskRating { get; set; } = string.Empty;
    public int RiskScore { get; set; }
    public DateTime ComputedAt { get; set; }
    public string ComputedBy { get; set; } = string.Empty;
    public string RiskRulesVersion { get; set; } = string.Empty;
    public bool IsCurrent { get; set; }
}

/// <summary>
/// Response DTO for risk history
/// </summary>
public class RiskHistoryResponse
{
    public Guid ClientId { get; set; }
    public List<RiskProfileResponse> Profiles { get; set; } = new();
    public int TotalAssessments { get; set; }
    public RiskTrendSummary? Trend { get; set; }
}

/// <summary>
/// Risk trend summary
/// </summary>
public class RiskTrendSummary
{
    public string CurrentRating { get; set; } = string.Empty;
    public string? PreviousRating { get; set; }
    public string Trend { get; set; } = "Stable"; // Increasing, Decreasing, Stable
    public int AverageScore { get; set; }
}
