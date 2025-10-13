using System.ComponentModel.DataAnnotations;

namespace IntelliFin.AdminService.Contracts.Requests;

public sealed class CreateAlertSilenceRequest
{
    [Required]
    [MinLength(1)]
    public List<AlertSilenceMatcherRequest> Matchers { get; set; } = new();

    [StringLength(500)]
    public string? Comment { get; set; }

    [StringLength(100)]
    public string? CreatedBy { get; set; }

    public DateTime? StartsAt { get; set; }

    public DateTime? EndsAt { get; set; }
}

public sealed class AlertSilenceMatcherRequest
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Value { get; set; } = string.Empty;

    public bool IsRegex { get; set; }
}
