using System;
using System.ComponentModel.DataAnnotations;

namespace IntelliFin.AdminService.Contracts.Requests;

public class MfaValidationRequest
{
    [Required]
    public Guid ChallengeId { get; set; }

    [Required]
    [RegularExpression("^[0-9]{6}$", ErrorMessage = "OTP code must be 6 digits")]
    public string OtpCode { get; set; } = string.Empty;
}
