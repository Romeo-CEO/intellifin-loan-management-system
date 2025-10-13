namespace IntelliFin.AdminService.Models;

public class BastionSession
{
    public long Id { get; set; }

    public Guid SessionId { get; set; } = Guid.NewGuid();

    public Guid? AccessRequestId { get; set; }
        = null;

    public string Username { get; set; } = string.Empty;

    public string ClientIp { get; set; } = string.Empty;

    public string BastionHost { get; set; } = string.Empty;

    public string? TargetHost { get; set; } = string.Empty;

    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    public DateTime? EndTime { get; set; }
        = null;

    public int? DurationSeconds { get; set; }
        = null;

    public string? RecordingPath { get; set; } = string.Empty;

    public long? RecordingSize { get; set; }
        = null;

    public string Status { get; set; } = "Active";

    public string? TerminationReason { get; set; } = string.Empty;

    public int CommandCount { get; set; }
        = 0;

    public BastionAccessRequest? AccessRequest { get; set; }
        = null;
}
