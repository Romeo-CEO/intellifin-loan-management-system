using System.ComponentModel.DataAnnotations;

namespace IntelliFin.Contracts.Requests;

public sealed record RoleAssignmentRequest
{
    [Required]
    [MaxLength(100)]
    public required string RoleName { get; init; }

    public bool? ConfirmedSodOverride { get; init; }
};