using System.ComponentModel.DataAnnotations;

namespace IntelliFin.IdentityService.Models;

public class LoginRequest
{
    [Required(ErrorMessage = "Username is required")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 100 characters")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "Password must be between 8 and 128 characters")]
    public string Password { get; set; } = string.Empty;

    public string? DeviceId { get; set; }
    public string? UserAgent { get; set; }
    public bool RememberMe { get; set; } = false;
    public string? TwoFactorCode { get; set; }
}