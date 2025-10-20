using System.ComponentModel.DataAnnotations;

namespace IntelliFin.IdentityService.Models;

public class ClientCredentialsRequest
{
    [Required]
    [StringLength(150, MinimumLength = 3)]
    public string ClientId { get; set; } = string.Empty;

    [Required]
    [MinLength(32)]
    public string ClientSecret { get; set; } = string.Empty;

    public string[]? Scopes { get; set; }
}
