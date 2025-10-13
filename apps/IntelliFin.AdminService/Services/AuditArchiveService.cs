using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using IntelliFin.AdminService.Data;
using IntelliFin.AdminService.Models;
using IntelliFin.AdminService.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel;
using Minio.DataModel.Args;
using Minio.DataModel.ObjectLock;

namespace IntelliFin.AdminService.Services;

public sealed class AuditArchiveService : IAuditArchiveService
{
    private readonly AdminDbContext _dbContext;
    private readonly IMinioClient _minioClient;
    private readonly IAuditService _auditService;
    private readonly ILogger<AuditArchiveService> _logger;
    private readonly JsonSerializerOptions _serializerOptions;

    private AuditArchiveOptions _archiveOptions;
    private MinioOptions _minioOptions;

    private const string ChainExportAction = "AuditArchiveExported";

    public AuditArchiveService(
        AdminDbContext dbContext,
        IMinioClient minioClient,
        IAuditService auditService,
        IOptionsMonitor<AuditArchiveOptions> archiveOptions,
        IOptionsMonitor<MinioOptions> minioOptions,
        ILogger<AuditArchiveService> logger)
    {
        _dbContext = dbContext;
        _minioClient = minioClient;
        _auditService = auditService;
        _logger = logger;
        _archiveOptions = archiveOptions.CurrentValue;
        _minioOptions = minioOptions.CurrentValue;
        archiveOptions.OnChange(options => _archiveOptions = options);
        minioOptions.OnChange(options => _minioOptions = options);
        _serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    public Task<bool> ArchiveExistsAsync(DateTime eventDate, CancellationToken cancellationToken)
    {
        var startOfDay = eventDate.Date;
        var endOfDay = startOfDay.AddDays(1).AddTicks(-1);
        return _dbContext.AuditArchiveMetadata
            .AsNoTracking()
            .AnyAsync(metadata => metadata.EventDateStart >= startOfDay && metadata.EventDateEnd <= endOfDay, cancellationToken);
    }

    public async Task<AuditArchiveResult> ExportDailyAuditEventsAsync(DateTime exportDate, CancellationToken cancellationToken)
    {
        if (!_archiveOptions.EnableExports)
        {
            _logger.LogInformation("Audit archive exports are disabled via configuration");
            return new AuditArchiveResult { Success = true, EventCount = 0 };
        }

        await EnsureArchiveBucketAsync(cancellationToken);

        var startOfDay = exportDate.Date;
        var endOfDay = startOfDay.AddDays(1).AddTicks(-1);

        var events = await _dbContext.AuditEvents
            .AsNoTracking()
            .Where(evt => evt.Timestamp >= startOfDay && evt.Timestamp <= endOfDay)
            .OrderBy(evt => evt.Timestamp)
            .ThenBy(evt => evt.Id)
            .ToListAsync(cancellationToken);

        if (events.Count == 0)
        {
            _logger.LogInformation("No audit events found for {ExportDate:yyyy-MM-dd}; skipping archive export", exportDate);
            return new AuditArchiveResult { Success = true, EventCount = 0 };
        }

        var bucketName = _archiveOptions.BucketName;
        var fileName = $"audit-events-{exportDate:yyyy-MM-dd}.jsonl.gz";
        var objectKey = $"{exportDate:yyyy}/{exportDate:MM}/{fileName}";
        var metadataObjectKey = objectKey.Replace(".jsonl.gz", "-metadata.json", StringComparison.OrdinalIgnoreCase);
        var verifyScriptKey = objectKey.Replace(".jsonl.gz", "-verify.py", StringComparison.OrdinalIgnoreCase);
        var tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-{fileName}");
        long uncompressedBytes = 0;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(tempFilePath)!);

            await using (var fileStream = File.Create(tempFilePath))
            await using (var gzipStream = new GZipStream(fileStream, CompressionLevel.Optimal))
            await using (var writer = new StreamWriter(gzipStream, Encoding.UTF8))
            {
                foreach (var evt in events)
                {
                    var exportLine = new
                    {
                        evt.EventId,
                        evt.Timestamp,
                        evt.Actor,
                        evt.Action,
                        evt.EntityType,
                        evt.EntityId,
                        evt.CorrelationId,
                        evt.IpAddress,
                        evt.UserAgent,
                        evt.EventData,
                        evt.PreviousEventHash,
                        evt.CurrentEventHash,
                        evt.IntegrityStatus
                    };

                    var json = JsonSerializer.Serialize(exportLine, _serializerOptions);
                    await writer.WriteLineAsync(json);
                    uncompressedBytes += Encoding.UTF8.GetByteCount(json) + 1; // include newline
                }

                await writer.FlushAsync();
            }

            var fileInfo = new FileInfo(tempFilePath);
            var compressionRatio = uncompressedBytes == 0
                ? 0m
                : Math.Round((decimal)fileInfo.Length / uncompressedBytes, 2, MidpointRounding.AwayFromZero);

            var retentionUntil = CalculateRetentionExpiry(exportDate);
            var retentionConfiguration = new ObjectRetentionConfiguration(retentionUntil, ObjectRetentionMode.COMPLIANCE);

            await _minioClient.PutObjectAsync(new PutObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectKey)
                .WithFileName(tempFilePath)
                .WithContentType("application/gzip")
                .WithRetentionConfiguration(retentionConfiguration)
                .WithHeaders(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["export-date"] = exportDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    ["event-count"] = events.Count.ToString(CultureInfo.InvariantCulture),
                    ["chain-start"] = events.First().CurrentEventHash ?? string.Empty,
                    ["chain-end"] = events.Last().CurrentEventHash ?? string.Empty,
                    ["chain-link"] = events.First().PreviousEventHash ?? string.Empty
                }), cancellationToken);

            await UploadMetadataDocumentAsync(bucketName, metadataObjectKey, fileName, objectKey, events, retentionUntil, retentionConfiguration, cancellationToken);
            await UploadOfflineVerifierAsync(bucketName, verifyScriptKey, fileName, cancellationToken, retentionConfiguration);

            ObjectStat? objectStat = null;
            try
            {
                objectStat = await _minioClient.StatObjectAsync(new StatObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectKey), cancellationToken);
            }
            catch (Exception statEx)
            {
                _logger.LogWarning(statEx, "Unable to retrieve replication status for {ObjectKey}", objectKey);
            }

            var archiveMetadata = new AuditArchiveMetadata
            {
                ArchiveId = Guid.NewGuid(),
                FileName = fileName,
                ObjectKey = objectKey,
                ExportDate = DateTime.UtcNow,
                EventDateStart = startOfDay,
                EventDateEnd = endOfDay,
                EventCount = events.Count,
                FileSize = fileInfo.Length,
                CompressionRatio = compressionRatio,
                ChainStartHash = events.First().CurrentEventHash,
                ChainEndHash = events.Last().CurrentEventHash,
                PreviousDayEndHash = events.First().PreviousEventHash,
                RetentionExpiryDate = retentionUntil,
                StorageLocation = "PRIMARY",
                ReplicationStatus = objectStat?.ReplicationStatus ?? "PENDING",
                LastReplicationCheckUtc = objectStat is null ? null : DateTime.UtcNow
            };

            await _dbContext.AuditArchiveMetadata.AddAsync(archiveMetadata, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await MarkEventsArchivedAsync(cancellationToken);

            await _auditService.LogEventAsync(new AuditEvent
            {
                Timestamp = DateTime.UtcNow,
                Actor = "system",
                Action = ChainExportAction,
                EntityType = "AuditArchive",
                EntityId = archiveMetadata.ArchiveId.ToString(),
                EventData = JsonSerializer.Serialize(new
                {
                    archiveMetadata.FileName,
                    archiveMetadata.EventDateStart,
                    archiveMetadata.EventDateEnd,
                    archiveMetadata.EventCount,
                    archiveMetadata.ObjectKey
                }, _serializerOptions)
            }, cancellationToken);
            await _auditService.FlushBufferAsync(cancellationToken);

            _logger.LogInformation(
                "Exported {Count} audit events to {ObjectKey} ({Size} bytes, ratio {Ratio})",
                archiveMetadata.EventCount,
                archiveMetadata.ObjectKey,
                archiveMetadata.FileSize,
                archiveMetadata.CompressionRatio);

            return new AuditArchiveResult
            {
                Success = true,
                EventCount = archiveMetadata.EventCount,
                FileName = archiveMetadata.FileName,
                ObjectKey = archiveMetadata.ObjectKey,
                FileSize = archiveMetadata.FileSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export audit events for {ExportDate:yyyy-MM-dd}", exportDate);
            return new AuditArchiveResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                EventCount = events.Count,
                FileName = fileName,
                ObjectKey = objectKey
            };
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                try
                {
                    File.Delete(tempFilePath);
                }
                catch (IOException ioEx)
                {
                    _logger.LogWarning(ioEx, "Failed to delete temporary archive file {Path}", tempFilePath);
                }
            }
        }
    }

    public async Task<IReadOnlyList<AuditArchiveMetadata>> SearchArchivesAsync(DateTime? startDate, DateTime? endDate, CancellationToken cancellationToken)
    {
        var query = _dbContext.AuditArchiveMetadata.AsNoTracking();

        if (startDate.HasValue)
        {
            query = query.Where(metadata => metadata.EventDateEnd >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(metadata => metadata.EventDateStart <= endDate.Value);
        }

        return await query
            .OrderByDescending(metadata => metadata.EventDateStart)
            .ThenByDescending(metadata => metadata.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<AuditArchiveMetadata?> GetArchiveAsync(Guid archiveId, CancellationToken cancellationToken)
        => _dbContext.AuditArchiveMetadata.FirstOrDefaultAsync(metadata => metadata.ArchiveId == archiveId, cancellationToken);

    public async Task<string> GenerateDownloadUrlAsync(AuditArchiveMetadata archive, CancellationToken cancellationToken)
    {
        var expirySeconds = Math.Clamp(_minioOptions.PresignedUrlExpirySeconds, 60, 86_400);
        var url = await _minioClient.PresignedGetObjectAsync(new PresignedGetObjectArgs()
            .WithBucket(_archiveOptions.BucketName)
            .WithObject(archive.ObjectKey)
            .WithExpiry(expirySeconds));
        return url;
    }

    public async Task UpdateAccessMetadataAsync(AuditArchiveMetadata archive, string? accessedBy, CancellationToken cancellationToken)
    {
        archive.LastAccessedAtUtc = DateTime.UtcNow;
        archive.LastAccessedBy = string.IsNullOrWhiteSpace(accessedBy) ? "unknown" : accessedBy;
        _dbContext.AuditArchiveMetadata.Update(archive);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditArchiveMetadata>> GetPendingReplicationAsync(CancellationToken cancellationToken)
    {
        var pendingStatuses = new[] { "PENDING", "FAILED", string.Empty, null };
        return await _dbContext.AuditArchiveMetadata
            .Where(metadata => pendingStatuses.Contains(metadata.ReplicationStatus))
            .OrderBy(metadata => metadata.EventDateStart)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateReplicationStatusAsync(AuditArchiveMetadata archive, string? replicationStatus, CancellationToken cancellationToken)
    {
        archive.ReplicationStatus = string.IsNullOrWhiteSpace(replicationStatus) ? "PENDING" : replicationStatus;
        archive.LastReplicationCheckUtc = DateTime.UtcNow;
        _dbContext.AuditArchiveMetadata.Update(archive);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureArchiveBucketAsync(CancellationToken cancellationToken)
    {
        var bucketExists = await _minioClient.BucketExistsAsync(new BucketExistsArgs().WithBucket(_archiveOptions.BucketName), cancellationToken);
        if (!bucketExists)
        {
            throw new InvalidOperationException($"MinIO bucket '{_archiveOptions.BucketName}' does not exist. Run the MinIO setup script before exporting audit archives.");
        }
    }

    private DateTime CalculateRetentionExpiry(DateTime exportDate)
    {
        var graceDays = Math.Max(0, _archiveOptions.MetadataRetentionGraceDays);
        var retention = exportDate.Date.AddDays(_archiveOptions.RetentionDays + graceDays);
        return DateTime.SpecifyKind(retention, DateTimeKind.Utc);
    }

    private async Task UploadMetadataDocumentAsync(
        string bucketName,
        string objectKey,
        string fileName,
        string archiveObjectKey,
        IReadOnlyList<AuditEvent> events,
        DateTime retentionUntil,
        ObjectRetentionConfiguration retentionConfiguration,
        CancellationToken cancellationToken)
    {
        var metadataDocument = new
        {
            fileName,
            objectKey = archiveObjectKey,
            exportedAtUtc = DateTime.UtcNow,
            eventCount = events.Count,
            firstEventId = events.First().EventId,
            lastEventId = events.Last().EventId,
            chainStartHash = events.First().CurrentEventHash,
            chainEndHash = events.Last().CurrentEventHash,
            previousDayEndHash = events.First().PreviousEventHash,
            retentionExpiresAtUtc = retentionUntil,
            instructions = "Use the companion verify.py script to validate the tamper-evident chain offline"
        };

        var payload = JsonSerializer.Serialize(metadataDocument, _serializerOptions);
        await using var metadataStream = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        await _minioClient.PutObjectAsync(new PutObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectKey)
            .WithStreamData(metadataStream)
            .WithObjectSize(metadataStream.Length)
            .WithContentType("application/json")
            .WithRetentionConfiguration(retentionConfiguration), cancellationToken);
    }

    private async Task UploadOfflineVerifierAsync(
        string bucketName,
        string objectKey,
        string archiveFileName,
        CancellationToken cancellationToken,
        ObjectRetentionConfiguration retentionConfiguration)
    {
        var script = """#!/usr/bin/env python3
"""Offline verification utility for IntelliFin audit archives."""
import gzip
import hashlib
import json
import sys
from pathlib import Path

if len(sys.argv) != 2:
    print("Usage: verify.py <path-to-audit-events.jsonl.gz>")
    sys.exit(1)

archive_path = Path(sys.argv[1])
if not archive_path.exists():
    print(f"Archive file {archive_path} not found")
    sys.exit(1)

previous_hash = None
line_number = 0

with gzip.open(archive_path, "rt", encoding="utf-8") as handle:
    for line in handle:
        line_number += 1
        payload = json.loads(line)
        chain_input = "".join([
            payload.get("PreviousEventHash", ""),
            payload.get("EventId", ""),
            payload.get("Timestamp", ""),
            payload.get("Actor", ""),
            payload.get("Action", ""),
            payload.get("EntityType", ""),
            payload.get("EntityId", ""),
            payload.get("EventData", ""),
        ])
        calculated = hashlib.sha256(chain_input.encode("utf-8")).hexdigest()
        current_hash = payload.get("CurrentEventHash")

        if current_hash != calculated:
            print(f"Hash mismatch detected at line {line_number}")
            sys.exit(2)

        if previous_hash is not None and payload.get("PreviousEventHash") != previous_hash:
            print(f"Chain break detected at line {line_number}")
            sys.exit(3)

        previous_hash = current_hash

print("Chain verification completed successfully.")
""";

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(script));
        await _minioClient.PutObjectAsync(new PutObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectKey)
            .WithStreamData(stream)
            .WithObjectSize(stream.Length)
            .WithContentType("text/x-python")
            .WithRetentionConfiguration(retentionConfiguration)
            .WithHeaders(new Dictionary<string, string>
            {
                ["archive"] = archiveFileName
            }), cancellationToken);
    }

    private async Task MarkEventsArchivedAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.Date.AddDays(-_archiveOptions.CleanupRetentionDays);
        var updated = await _dbContext.AuditEvents
            .Where(evt => evt.Timestamp < cutoff && evt.MigrationSource != "ARCHIVED")
            .ExecuteUpdateAsync(setters => setters.SetProperty(evt => evt.MigrationSource, _ => "ARCHIVED"), cancellationToken);

        if (updated > 0)
        {
            _logger.LogInformation("Marked {Count} audit events as ARCHIVED", updated);
        }
    }
}
