using System.ComponentModel.DataAnnotations;

namespace IntelliFin.AdminService.Contracts.Requests;

public sealed record SodExceptionRequest
{
    [Required]
    [MaxLength(100)]
    public required string UserId { get; init; }

    [Required]
    [MaxLength(200)]
    public required string UserName { get; init; }

    [Required]
    [MaxLength(100)]
    public required string RequestedRole { get; init; }

    [Required]
    [MinLength(1)]
    public required IReadOnlyCollection<string> ConflictingRoles { get; init; }

    [Required]
    [StringLength(1000, MinimumLength = 50)]
    public required string BusinessJustification { get; init; }

    [Range(1, 90)]
    public int ExceptionDuration { get; init; }
};
