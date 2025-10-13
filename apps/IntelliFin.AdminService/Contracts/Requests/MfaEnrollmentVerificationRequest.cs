using System.ComponentModel.DataAnnotations;

namespace IntelliFin.AdminService.Contracts.Requests;

public class MfaEnrollmentVerificationRequest
{
    [Required]
    public string SecretKey { get; set; } = string.Empty;

    [Required]
    [RegularExpression("^[0-9]{6}$", ErrorMessage = "OTP code must be 6 digits")]
    public string OtpCode { get; set; } = string.Empty;
}
