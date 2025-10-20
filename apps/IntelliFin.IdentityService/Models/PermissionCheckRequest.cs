using System.ComponentModel.DataAnnotations;

namespace IntelliFin.IdentityService.Models;

public record PermissionCheckRequest
{
    [Required]
    public string UserId { get; init; } = string.Empty;

    [Required]
    public string Permission { get; init; } = string.Empty;

    public PermissionContext? Context { get; init; }
}
