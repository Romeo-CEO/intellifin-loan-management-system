using System.ComponentModel.DataAnnotations;

namespace IntelliFin.AdminService.Contracts.Requests;

public sealed record ResetPasswordRequest
{
    [Required]
    [MinLength(8)]
    public required string TemporaryPassword { get; init; }

    public bool Temporary { get; init; } = true;
};
