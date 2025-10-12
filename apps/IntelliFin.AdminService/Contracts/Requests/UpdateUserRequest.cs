using System.ComponentModel.DataAnnotations;

namespace IntelliFin.AdminService.Contracts.Requests;

public sealed record UpdateUserRequest
{
    [MaxLength(254)]
    public string? Email { get; init; }

    [MaxLength(100)]
    public string? FirstName { get; init; }

    [MaxLength(100)]
    public string? LastName { get; init; }

    public bool? Enabled { get; init; }

    public bool? EmailVerified { get; init; }

    public IDictionary<string, string>? Attributes { get; init; }
};
