using System;

namespace IntelliFin.AdminService.Models;

public class OfflineMergeHistory
{
    public int Id { get; set; }
    public Guid MergeId { get; set; }
    public DateTime MergeTimestamp { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string OfflineSessionId { get; set; } = string.Empty;
    public int EventsReceived { get; set; }
    public int EventsMerged { get; set; }
    public int DuplicatesSkipped { get; set; }
    public int ConflictsDetected { get; set; }
    public int EventsReHashed { get; set; }
    public int MergeDurationMs { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorDetails { get; set; }
}
