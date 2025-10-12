using System.ComponentModel.DataAnnotations;

namespace IntelliFin.IdentityService.Models;

public class RevokeTokenRequest
{
    [Required(ErrorMessage = "Refresh token is required")]
    public string RefreshToken { get; set; } = string.Empty;
}
