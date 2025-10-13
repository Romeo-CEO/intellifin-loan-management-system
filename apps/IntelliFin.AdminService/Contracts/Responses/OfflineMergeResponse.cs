using System;

namespace IntelliFin.AdminService.Contracts.Responses;

public sealed class OfflineMergeResponse
{
    public Guid MergeId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int EventsReceived { get; set; }
    public int EventsMerged { get; set; }
    public int DuplicatesSkipped { get; set; }
    public int ConflictsDetected { get; set; }
    public int EventsReHashed { get; set; }
    public int MergeDurationMs { get; set; }
    public string Message { get; set; } = string.Empty;
}
