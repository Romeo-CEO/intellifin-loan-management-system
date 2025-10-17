using System.ComponentModel.DataAnnotations;

namespace IntelliFin.IdentityService.Models;

public class ServiceAccountCreateRequest
{
    [Required]
    [StringLength(200, MinimumLength = 3)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public string[] Scopes { get; set; } = Array.Empty<string>();

    [StringLength(200)]
    public string? ActorId { get; set; }

    [StringLength(500)]
    public string? Reason { get; set; }
}
