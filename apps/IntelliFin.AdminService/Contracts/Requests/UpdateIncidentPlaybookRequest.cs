using System.ComponentModel.DataAnnotations;

namespace IntelliFin.AdminService.Contracts.Requests;

public sealed class UpdateIncidentPlaybookRequest
{
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Summary { get; set; }

    [Required]
    public string DiagnosisSteps { get; set; } = string.Empty;

    [Required]
    public string ResolutionSteps { get; set; } = string.Empty;

    [Required]
    public string EscalationPath { get; set; } = string.Empty;

    [Url]
    [StringLength(500)]
    public string? LinkedRunbookUrl { get; set; }

    [Required]
    [StringLength(100)]
    public string Owner { get; set; } = string.Empty;

    [StringLength(100)]
    public string? AutomationProcessKey { get; set; }

    public bool IsActive { get; set; } = true;
}
