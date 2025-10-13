using System.ComponentModel.DataAnnotations;

namespace IntelliFin.AdminService.Contracts.Requests;

public sealed record ConfigChangeRequest
{
    [Required]
    [MaxLength(200)]
    public required string ConfigKey { get; init; }

    [Required]
    [MaxLength(1000)]
    public required string NewValue { get; init; }

    [Required]
    [MaxLength(1000)]
    public required string Justification { get; init; }

    [MaxLength(50)]
    public string? Category { get; init; }
};
