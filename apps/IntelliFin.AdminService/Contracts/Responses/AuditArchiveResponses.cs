namespace IntelliFin.AdminService.Contracts.Responses;

public sealed class AuditArchiveItemResponse
{
    public Guid ArchiveId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ObjectKey { get; set; } = string.Empty;
    public DateTime EventDateStart { get; set; }
        = DateTime.UtcNow;
    public DateTime EventDateEnd { get; set; }
        = DateTime.UtcNow;
    public int EventCount { get; set; }
    public long FileSize { get; set; }
    public decimal CompressionRatio { get; set; }
    public string? ChainStartHash { get; set; }
    public string? ChainEndHash { get; set; }
    public string? PreviousDayEndHash { get; set; }
    public DateTime RetentionExpiryDate { get; set; }
    public string StorageLocation { get; set; } = string.Empty;
    public string ReplicationStatus { get; set; } = string.Empty;
    public DateTime? LastReplicationCheckUtc { get; set; }
        = null;
    public DateTime? LastAccessedAtUtc { get; set; }
        = null;
    public string? LastAccessedBy { get; set; }
        = null;
}

public sealed class AuditArchiveSearchResponse
{
    public List<AuditArchiveItemResponse> Archives { get; set; } = new();
    public int TotalCount { get; set; }
    public long TotalEvents { get; set; }
    public long TotalSize { get; set; }
}

public sealed class AuditArchiveDownloadResponse
{
    public string DownloadUrl { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public int ExpiresInSeconds { get; set; }
    public DateTime RetentionExpiryDate { get; set; }
}
