using System.ComponentModel.DataAnnotations;

namespace IntelliFin.Contracts.Requests;

public sealed record CreateUserRequest
{
    [Required]
    [MaxLength(254)]
    public required string Username { get; init; }

    [EmailAddress]
    [Required]
    [MaxLength(254)]
    public required string Email { get; init; }

    [MaxLength(100)]
    public string? FirstName { get; init; }

    [MaxLength(100)]
    public string? LastName { get; init; }

    public bool Enabled { get; init; } = true;

    public bool EmailVerified { get; init; }

    public IDictionary<string, string>? Attributes { get; init; }
};