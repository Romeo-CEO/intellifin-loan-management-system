using System.ComponentModel.DataAnnotations;

namespace IntelliFin.AdminService.Contracts.Requests;

public sealed record SodExceptionReviewDto
{
    [Required]
    [StringLength(1000, MinimumLength = 10)]
    public required string Comments { get; init; }
};
