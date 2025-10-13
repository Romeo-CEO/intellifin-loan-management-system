using System;

namespace IntelliFin.AdminService.Models;

public sealed class OfflineMergeResult
{
    public Guid MergeId { get; set; }
    public string Status { get; set; } = "SUCCESS";
    public int EventsReceived { get; set; }
    public int EventsMerged { get; set; }
    public int DuplicatesSkipped { get; set; }
    public int ConflictsDetected { get; set; }
    public int EventsReHashed { get; set; }
    public int MergeDurationMs { get; set; }
    public string? ErrorDetails { get; set; }
}
