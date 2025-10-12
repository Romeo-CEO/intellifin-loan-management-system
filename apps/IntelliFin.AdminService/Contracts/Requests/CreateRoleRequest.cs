using System.ComponentModel.DataAnnotations;

namespace IntelliFin.AdminService.Contracts.Requests;

public sealed record CreateRoleRequest
{
    [Required]
    [MaxLength(255)]
    public required string Name { get; init; }

    [MaxLength(1024)]
    public string? Description { get; init; }
};
