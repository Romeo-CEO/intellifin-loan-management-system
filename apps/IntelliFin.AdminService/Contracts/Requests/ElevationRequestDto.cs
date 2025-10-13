using System.ComponentModel.DataAnnotations;

namespace IntelliFin.AdminService.Contracts.Requests;

public sealed record ElevationRequestDto
{
    [Required]
    [MinLength(1)]
    public required IReadOnlyCollection<string> RequestedRoles { get; init; }

    [Required]
    [StringLength(1000, MinimumLength = 20)]
    public required string Justification { get; init; }

    [Range(1, 480)]
    public int Duration { get; init; }
};
