using System.ComponentModel.DataAnnotations;

namespace IntelliFin.AdminService.Contracts.Requests;

public sealed record ElevationRejectionDto
{
    [Required]
    [StringLength(500, MinimumLength = 10)]
    public required string Reason { get; init; }
};
