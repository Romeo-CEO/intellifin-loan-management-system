using System.ComponentModel.DataAnnotations;

namespace IntelliFin.AdminService.Contracts.Requests;

public sealed record SodValidationRequest
{
    [Required]
    [MaxLength(100)]
    public required string UserId { get; init; }

    [Required]
    [MaxLength(100)]
    public required string ProposedRole { get; init; }
};
