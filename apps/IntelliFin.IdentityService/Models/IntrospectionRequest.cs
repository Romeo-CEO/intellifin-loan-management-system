using System.ComponentModel.DataAnnotations;

namespace IntelliFin.IdentityService.Models;

public record IntrospectionRequest
{
    [Required]
    public string Token { get; init; } = string.Empty;

    public string? TokenTypeHint { get; init; }
}
