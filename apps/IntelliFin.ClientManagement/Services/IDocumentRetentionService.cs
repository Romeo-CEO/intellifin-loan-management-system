using IntelliFin.Shared.Infrastructure;

namespace IntelliFin.ClientManagement.Services;

/// <summary>
/// Service for managing document lifecycle and retention policies
/// Implements BoZ 10-year retention requirement
/// </summary>
public interface IDocumentRetentionService
{
    /// <summary>
    /// Archives documents that have exceeded retention period
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of documents archived</returns>
    Task<Result<int>> ArchiveExpiredDocumentsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Archives a specific document manually
    /// </summary>
    /// <param name="documentId">Document ID</param>
    /// <param name="archivedBy">User archiving the document</param>
    /// <param name="reason">Reason for archival</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<Result<bool>> ArchiveDocumentAsync(
        Guid documentId,
        string archivedBy,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores an archived document
    /// </summary>
    /// <param name="documentId">Document ID</param>
    /// <param name="restoredBy">User restoring the document</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<Result<bool>> RestoreDocumentAsync(
        Guid documentId,
        string restoredBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets list of documents eligible for archival
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<Result<List<DocumentRetentionInfo>>> GetEligibleForArchivalAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets retention statistics
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<Result<RetentionStatistics>> GetRetentionStatisticsAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Information about document retention status
/// </summary>
public class DocumentRetentionInfo
{
    /// <summary>
    /// Document ID
    /// </summary>
    public Guid DocumentId { get; set; }

    /// <summary>
    /// Client ID
    /// </summary>
    public Guid ClientId { get; set; }

    /// <summary>
    /// Document type
    /// </summary>
    public string DocumentType { get; set; } = string.Empty;

    /// <summary>
    /// Upload date
    /// </summary>
    public DateTime UploadedAt { get; set; }

    /// <summary>
    /// Retention expiry date
    /// </summary>
    public DateTime RetentionUntil { get; set; }

    /// <summary>
    /// Days until retention expires
    /// </summary>
    public int DaysUntilExpiry { get; set; }

    /// <summary>
    /// Days since retention expired (negative if not yet expired)
    /// </summary>
    public int DaysSinceExpiry { get; set; }

    /// <summary>
    /// Whether document is archived
    /// </summary>
    public bool IsArchived { get; set; }

    /// <summary>
    /// File size in bytes
    /// </summary>
    public long FileSizeBytes { get; set; }
}

/// <summary>
/// Retention statistics
/// </summary>
public class RetentionStatistics
{
    /// <summary>
    /// Total documents in system
    /// </summary>
    public int TotalDocuments { get; set; }

    /// <summary>
    /// Active (non-archived) documents
    /// </summary>
    public int ActiveDocuments { get; set; }

    /// <summary>
    /// Archived documents
    /// </summary>
    public int ArchivedDocuments { get; set; }

    /// <summary>
    /// Documents eligible for archival (retention expired)
    /// </summary>
    public int EligibleForArchival { get; set; }

    /// <summary>
    /// Documents expiring within 30 days
    /// </summary>
    public int ExpiringWithin30Days { get; set; }

    /// <summary>
    /// Documents expiring within 90 days
    /// </summary>
    public int ExpiringWithin90Days { get; set; }

    /// <summary>
    /// Total storage used by active documents (bytes)
    /// </summary>
    public long ActiveStorageBytes { get; set; }

    /// <summary>
    /// Total storage used by archived documents (bytes)
    /// </summary>
    public long ArchivedStorageBytes { get; set; }

    /// <summary>
    /// Oldest active document date
    /// </summary>
    public DateTime? OldestActiveDocument { get; set; }

    /// <summary>
    /// Most recent archival date
    /// </summary>
    public DateTime? MostRecentArchival { get; set; }
}
