using System.ComponentModel.DataAnnotations;

namespace IntelliFin.AdminService.Contracts.Requests;

public sealed record AssignRolesRequest
{
    [Required]
    [MinLength(1)]
    public required IReadOnlyCollection<string> Roles { get; init; }
};
