using System.ComponentModel.DataAnnotations;

namespace IntelliFin.AdminService.Contracts.Requests;

public class BastionAccessRequestInput
{
    [Required]
    [RegularExpression("^(dev|staging|production)$", ErrorMessage = "Environment must be dev, staging, or production.")]
    public string Environment { get; set; } = "staging";

    [Range(1, 24)]
    public int AccessDurationHours { get; set; } = 2;

    [Required]
    [MinLength(50)]
    public string Justification { get; set; } = string.Empty;

    public IReadOnlyCollection<string>? TargetHosts { get; set; }
        = Array.Empty<string>();
}
