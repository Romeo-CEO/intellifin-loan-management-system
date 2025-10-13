using System.ComponentModel.DataAnnotations;

namespace IntelliFin.AdminService.Contracts.Requests;

public sealed record ConfigChangeRejectionDto
{
    [Required]
    [MinLength(20)]
    [MaxLength(500)]
    public required string Reason { get; init; }
};
