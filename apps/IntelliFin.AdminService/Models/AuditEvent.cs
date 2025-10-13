using System;

namespace IntelliFin.AdminService.Models;

public class AuditEvent
{
    public long Id { get; set; }
    public Guid EventId { get; set; }
    public DateTime Timestamp { get; set; }
    public string Actor { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? CorrelationId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? EventData { get; set; }
    public string? MigrationSource { get; set; }
    public string? PreviousEventHash { get; set; }
    public string? CurrentEventHash { get; set; }
    public string IntegrityStatus { get; set; } = "UNVERIFIED";
    public bool IsGenesisEvent { get; set; }
    public DateTime? LastVerifiedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsOfflineEvent { get; set; }
    public string? OfflineDeviceId { get; set; }
    public string? OfflineSessionId { get; set; }
    public Guid? OfflineMergeId { get; set; }
    public string? OriginalHash { get; set; }
}
