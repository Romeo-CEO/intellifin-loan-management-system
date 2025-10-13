using System.ComponentModel.DataAnnotations;

namespace IntelliFin.AdminService.Contracts.Requests;

public class EmergencyAccessRequest
{
    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string IncidentTicketId { get; set; } = string.Empty;

    [Required]
    [MinLength(50)]
    public string Justification { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string ApproverOneId { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string ApproverTwoId { get; set; } = string.Empty;

    public IReadOnlyCollection<string>? TargetHosts { get; set; }
        = Array.Empty<string>();
}
