using System.ComponentModel.DataAnnotations;

namespace IntelliFin.Contracts.Requests;

public sealed record UpdateRoleRequest
{
    [MaxLength(255)]
    public string? Name { get; init; }

    [MaxLength(1024)]
    public string? Description { get; init; }
};