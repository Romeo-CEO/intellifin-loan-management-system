using System.ComponentModel.DataAnnotations;

namespace IntelliFin.AdminService.Contracts.Requests;

public sealed record ConfigPolicyUpdateDto
{
    [MaxLength(50)]
    public string? Category { get; init; }

    public bool? RequiresApproval { get; init; }

    [MaxLength(20)]
    public string? Sensitivity { get; init; }

    [MaxLength(500)]
    public string? AllowedValuesRegex { get; init; }

    public string? AllowedValuesList { get; init; }

    [MaxLength(1000)]
    public string? Description { get; init; }
};
