namespace IntelliFin.CreditAssessmentService.Models;

/// <summary>
/// Request body for manual override operations.
/// </summary>
public sealed class ManualOverrideRequestDto
{
    public string Officer { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
}
