using System.ComponentModel.DataAnnotations;

namespace IntelliFin.AdminService.Contracts.Requests;

public sealed record SodPolicyUpdateDto
{
    [Required]
    public bool Enabled { get; init; }

    [Required]
    [MaxLength(20)]
    public required string Severity { get; init; }

    [StringLength(500, MinimumLength = 10)]
    public string? ConflictDescription { get; init; }
};
