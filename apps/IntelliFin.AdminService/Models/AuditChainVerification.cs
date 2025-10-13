namespace IntelliFin.AdminService.Models;

public class AuditChainVerification
{
    public int Id { get; set; }
    public Guid VerificationId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int EventsVerified { get; set; }
    public string ChainStatus { get; set; } = "UNVERIFIED";
    public long? BrokenEventId { get; set; }
    public DateTime? BrokenEventTimestamp { get; set; }
    public string InitiatedBy { get; set; } = string.Empty;
    public int VerificationDurationMs { get; set; }
}
