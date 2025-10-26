using IntelliFin.ClientManagement.Infrastructure.Persistence;
using IntelliFin.Shared.Audit;
using IntelliFin.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace IntelliFin.ClientManagement.Services;

/// <summary>
/// Implementation of document retention service
/// Manages 10-year document lifecycle per BoZ requirements
/// </summary>
public class DocumentRetentionService : IDocumentRetentionService
{
    private readonly ClientManagementDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ILogger<DocumentRetentionService> _logger;

    // BoZ retention period: 10 years
    private const int RetentionPeriodYears = 10;

    public DocumentRetentionService(
        ClientManagementDbContext context,
        IAuditService auditService,
        ILogger<DocumentRetentionService> logger)
    {
        _context = context;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<Result<int>> ArchiveExpiredDocumentsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting automated document archival process");

            var now = DateTime.UtcNow;

            // Find documents that have exceeded retention period and are not yet archived
            var expiredDocuments = await _context.ClientDocuments
                .Where(d => !d.IsArchived && d.RetentionUntil < now)
                .ToListAsync(cancellationToken);

            if (!expiredDocuments.Any())
            {
                _logger.LogInformation("No documents eligible for archival");
                return Result<int>.Success(0);
            }

            _logger.LogInformation(
                "Found {Count} documents eligible for archival",
                expiredDocuments.Count);

            // Archive each document
            foreach (var document in expiredDocuments)
            {
                document.IsArchived = true;
                document.ArchivedAt = now;
                document.ArchivedBy = "system-automated";
                document.ArchivalReason = "Retention period expired (10 years)";

                // Audit log
                await _auditService.LogEventAsync(
                    "Document.Archived",
                    "ClientManagement",
                    document.Id.ToString(),
                    "system-automated",
                    new
                    {
                        documentId = document.Id,
                        clientId = document.ClientId,
                        documentType = document.DocumentType,
                        uploadedAt = document.UploadedAt,
                        retentionUntil = document.RetentionUntil,
                        reason = "Automated archival - retention expired"
                    });
            }

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Successfully archived {Count} documents",
                expiredDocuments.Count);

            return Result<int>.Success(expiredDocuments.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during automated document archival");
            return Result<int>.Failure($"Archival failed: {ex.Message}");
        }
    }

    public async Task<Result<bool>> ArchiveDocumentAsync(
        Guid documentId,
        string archivedBy,
        string reason,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Manually archiving document {DocumentId} by {User}",
                documentId, archivedBy);

            var document = await _context.ClientDocuments
                .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);

            if (document == null)
            {
                return Result<bool>.Failure($"Document not found: {documentId}");
            }

            if (document.IsArchived)
            {
                return Result<bool>.Failure("Document is already archived");
            }

            // Archive the document
            document.IsArchived = true;
            document.ArchivedAt = DateTime.UtcNow;
            document.ArchivedBy = archivedBy;
            document.ArchivalReason = reason;

            await _context.SaveChangesAsync(cancellationToken);

            // Audit log
            await _auditService.LogEventAsync(
                "Document.ArchivedManually",
                "ClientManagement",
                document.Id.ToString(),
                archivedBy,
                new
                {
                    documentId = document.Id,
                    clientId = document.ClientId,
                    documentType = document.DocumentType,
                    reason
                });

            _logger.LogInformation(
                "Document {DocumentId} archived successfully",
                documentId);

            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error archiving document {DocumentId}",
                documentId);
            return Result<bool>.Failure($"Archival failed: {ex.Message}");
        }
    }

    public async Task<Result<bool>> RestoreDocumentAsync(
        Guid documentId,
        string restoredBy,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Restoring document {DocumentId} by {User}",
                documentId, restoredBy);

            var document = await _context.ClientDocuments
                .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);

            if (document == null)
            {
                return Result<bool>.Failure($"Document not found: {documentId}");
            }

            if (!document.IsArchived)
            {
                return Result<bool>.Failure("Document is not archived");
            }

            if (!document.CanRestore)
            {
                return Result<bool>.Failure("Document cannot be restored");
            }

            // Restore the document
            document.IsArchived = false;
            document.RestoredAt = DateTime.UtcNow;
            document.RestoredBy = restoredBy;

            await _context.SaveChangesAsync(cancellationToken);

            // Audit log
            await _auditService.LogEventAsync(
                "Document.Restored",
                "ClientManagement",
                document.Id.ToString(),
                restoredBy,
                new
                {
                    documentId = document.Id,
                    clientId = document.ClientId,
                    documentType = document.DocumentType,
                    archivedAt = document.ArchivedAt,
                    archivedBy = document.ArchivedBy
                });

            _logger.LogInformation(
                "Document {DocumentId} restored successfully",
                documentId);

            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error restoring document {DocumentId}",
                documentId);
            return Result<bool>.Failure($"Restore failed: {ex.Message}");
        }
    }

    public async Task<Result<List<DocumentRetentionInfo>>> GetEligibleForArchivalAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var now = DateTime.UtcNow;

            var eligibleDocuments = await _context.ClientDocuments
                .Where(d => !d.IsArchived && d.RetentionUntil < now)
                .Select(d => new DocumentRetentionInfo
                {
                    DocumentId = d.Id,
                    ClientId = d.ClientId,
                    DocumentType = d.DocumentType,
                    UploadedAt = d.UploadedAt,
                    RetentionUntil = d.RetentionUntil,
                    DaysUntilExpiry = (int)(d.RetentionUntil - now).TotalDays,
                    DaysSinceExpiry = (int)(now - d.RetentionUntil).TotalDays,
                    IsArchived = d.IsArchived,
                    FileSizeBytes = d.FileSizeBytes
                })
                .ToListAsync(cancellationToken);

            return Result<List<DocumentRetentionInfo>>.Success(eligibleDocuments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting eligible documents for archival");
            return Result<List<DocumentRetentionInfo>>.Failure($"Query failed: {ex.Message}");
        }
    }

    public async Task<Result<RetentionStatistics>> GetRetentionStatisticsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var now = DateTime.UtcNow;
            var in30Days = now.AddDays(30);
            var in90Days = now.AddDays(90);

            var allDocuments = await _context.ClientDocuments.ToListAsync(cancellationToken);

            var stats = new RetentionStatistics
            {
                TotalDocuments = allDocuments.Count,
                ActiveDocuments = allDocuments.Count(d => !d.IsArchived),
                ArchivedDocuments = allDocuments.Count(d => d.IsArchived),
                EligibleForArchival = allDocuments.Count(d => !d.IsArchived && d.RetentionUntil < now),
                ExpiringWithin30Days = allDocuments.Count(d => !d.IsArchived && d.RetentionUntil >= now && d.RetentionUntil <= in30Days),
                ExpiringWithin90Days = allDocuments.Count(d => !d.IsArchived && d.RetentionUntil >= now && d.RetentionUntil <= in90Days),
                ActiveStorageBytes = allDocuments.Where(d => !d.IsArchived).Sum(d => d.FileSizeBytes),
                ArchivedStorageBytes = allDocuments.Where(d => d.IsArchived).Sum(d => d.FileSizeBytes),
                OldestActiveDocument = allDocuments.Where(d => !d.IsArchived).Any()
                    ? allDocuments.Where(d => !d.IsArchived).Min(d => d.UploadedAt)
                    : (DateTime?)null,
                MostRecentArchival = allDocuments.Where(d => d.IsArchived && d.ArchivedAt.HasValue).Any()
                    ? allDocuments.Where(d => d.IsArchived && d.ArchivedAt.HasValue).Max(d => d.ArchivedAt!.Value)
                    : (DateTime?)null
            };

            return Result<RetentionStatistics>.Success(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating retention statistics");
            return Result<RetentionStatistics>.Failure($"Statistics failed: {ex.Message}");
        }
    }
}
