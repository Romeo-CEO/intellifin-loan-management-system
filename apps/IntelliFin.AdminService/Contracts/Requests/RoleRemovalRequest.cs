using System.ComponentModel.DataAnnotations;

namespace IntelliFin.AdminService.Contracts.Requests;

public sealed record RoleRemovalRequest
{
    [Required]
    [MaxLength(100)]
    public required string RoleName { get; init; }

    [MaxLength(500)]
    public string? Reason { get; init; }
};
