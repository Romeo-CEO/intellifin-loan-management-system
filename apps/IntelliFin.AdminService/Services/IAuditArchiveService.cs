using IntelliFin.AdminService.Models;

namespace IntelliFin.AdminService.Services;

public interface IAuditArchiveService
{
    Task<bool> ArchiveExistsAsync(DateTime eventDate, CancellationToken cancellationToken);
    Task<AuditArchiveResult> ExportDailyAuditEventsAsync(DateTime exportDate, CancellationToken cancellationToken);
    Task<IReadOnlyList<AuditArchiveMetadata>> SearchArchivesAsync(DateTime? startDate, DateTime? endDate, CancellationToken cancellationToken);
    Task<AuditArchiveMetadata?> GetArchiveAsync(Guid archiveId, CancellationToken cancellationToken);
    Task<string> GenerateDownloadUrlAsync(AuditArchiveMetadata archive, CancellationToken cancellationToken);
    Task UpdateAccessMetadataAsync(AuditArchiveMetadata archive, string? accessedBy, CancellationToken cancellationToken);
    Task<IReadOnlyList<AuditArchiveMetadata>> GetPendingReplicationAsync(CancellationToken cancellationToken);
    Task UpdateReplicationStatusAsync(AuditArchiveMetadata archive, string? replicationStatus, CancellationToken cancellationToken);
}
