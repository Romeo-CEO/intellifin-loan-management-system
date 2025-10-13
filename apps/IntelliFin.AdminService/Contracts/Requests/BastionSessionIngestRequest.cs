using System.ComponentModel.DataAnnotations;

namespace IntelliFin.AdminService.Contracts.Requests;

public class BastionSessionIngestRequest
{
    [Required]
    public Guid SessionId { get; set; }

    public Guid? AccessRequestId { get; set; }
        = null;

    [Required]
    [StringLength(200)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string ClientIp { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string BastionHost { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string RecordingPath { get; set; } = string.Empty;

    public long? RecordingSize { get; set; }
        = null;

    public DateTime? StartTime { get; set; }
        = DateTime.UtcNow;

    public DateTime? EndTime { get; set; }
        = DateTime.UtcNow;

    public string? TargetHost { get; set; }
        = string.Empty;

    public string? Status { get; set; } = "Completed";

    public int? CommandCount { get; set; }
        = null;
}
