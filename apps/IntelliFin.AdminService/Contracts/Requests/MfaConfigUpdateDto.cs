using System.ComponentModel.DataAnnotations;

namespace IntelliFin.AdminService.Contracts.Requests;

public class MfaConfigUpdateDto
{
    [Required]
    public bool RequiresMfa { get; set; }

    [Range(1, 120)]
    public int TimeoutMinutes { get; set; } = 15;
}
