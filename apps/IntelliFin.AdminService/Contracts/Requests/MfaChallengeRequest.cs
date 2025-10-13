using System.ComponentModel.DataAnnotations;

namespace IntelliFin.AdminService.Contracts.Requests;

public class MfaChallengeRequest
{
    [Required]
    [MaxLength(200)]
    public string Operation { get; set; } = string.Empty;
}
