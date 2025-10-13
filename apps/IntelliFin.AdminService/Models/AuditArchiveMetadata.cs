namespace IntelliFin.AdminService.Models;

public sealed class AuditArchiveMetadata
{
    public int Id { get; set; }
    public Guid ArchiveId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ObjectKey { get; set; } = string.Empty;
    public DateTime ExportDate { get; set; }
        = DateTime.UtcNow;
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
    public string StorageLocation { get; set; } = "PRIMARY";
    public string ReplicationStatus { get; set; } = "PENDING";
    public DateTime? LastReplicationCheckUtc { get; set; }
        = null;
    public DateTime? LastAccessedAtUtc { get; set; }
        = null;
    public string? LastAccessedBy { get; set; }
        = null;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
