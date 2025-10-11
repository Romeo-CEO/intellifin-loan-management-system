# Story 1.16: MinIO WORM Audit Storage Integration

## Story Metadata

| Field | Value |
|-------|-------|
| **Story ID** | 1.16 |
| **Epic** | System Administration Control Plane Enhancement |
| **Phase** | Phase 3: Audit & Compliance |
| **Sprint** | Sprint 6 |
| **Story Points** | 8 |
| **Estimated Effort** | 5-7 days |
| **Priority** | P0 (Critical for BoZ compliance) |
| **Status** | 📋 Backlog |
| **Assigned To** | TBD |
| **Dependencies** | Story 1.15 (Tamper-evident chain), Story 1.14 (Centralized audit) |
| **Blocks** | Story 1.33 (DR runbooks) |

---

## User Story

**As a** Compliance Officer,  
**I want** audit logs stored in MinIO with WORM object locking and 10-year retention,
**so that** I comply with Bank of Zambia audit retention requirements with immutable storage guarantees.

---

## Business Value

WORM (Write-Once-Read-Many) storage provides immutable audit storage critical for regulatory compliance:

- **Regulatory Compliance**: Bank of Zambia mandates 10-year audit retention with immutability guarantees
- **Legal Admissibility**: WORM storage provides legally defensible evidence that audit logs haven't been altered
- **Ransomware Protection**: Immutable storage prevents encryption or deletion by malware
- **Data Sovereignty**: MinIO deployed in-country satisfies Zambian data residency requirements
- **Cost Efficiency**: Cold storage in MinIO more cost-effective than keeping all audit data in SQL Server hot storage

This implements SEC Rule 17a-4 compliant storage architecture.

---

## Acceptance Criteria

### AC1: MinIO Bucket with Object Lock Enabled
**Given** MinIO server is operational  
**When** creating audit storage bucket  
**Then**:
- Bucket `audit-logs` created with Object Lock enabled
- Object Lock mode set to `COMPLIANCE` (cannot be overridden, even by admin)
- Default retention period: 3654 days (10 years per BoZ requirement)
- Versioning enabled on bucket
- Bucket lifecycle policy configured for automatic retention enforcement

### AC2: Daily Audit Export to MinIO
**Given** Audit events accumulated in SQL Server (hot storage)  
**When** daily export job executes (midnight UTC)  
**Then**:
- Previous day's audit events exported as JSONL (JSON Lines) file
- File naming: `audit-events-YYYY-MM-DD.jsonl.gz` (gzipped for compression)
- File includes tamper-evident hash chain data for offline verification
- Object uploaded to MinIO with WORM retention lock applied
- Export metadata stored: file name, object key, event count, date range
- SQL Server audit events older than 90 days marked for archival cleanup

### AC3: Tamper-Evident Chain Export
**Given** Audit export includes hash chain data  
**When** exporting to MinIO  
**Then**:
- Each JSONL line includes `PreviousEventHash` and `CurrentEventHash`
- Chain continuity preserved across daily export files
- First event of day links to last event of previous day (cross-file chain)
- Metadata file `chain-metadata.json` exported with each daily file
- Offline verification script included in export for regulatory audit

### AC4: MinIO Access Logging
**Given** Audit archives stored in MinIO  
**When** any user accesses archived audit data  
**Then**:
- MinIO server-side access logs enabled
- Access logs capture: accessor identity, timestamp, object accessed, operation (read/download)
- Access logs themselves stored in separate WORM bucket `audit-access-logs`
- Unauthorized access attempts logged and alerted
- Compliance dashboard shows audit archive access history

### AC5: Disaster Recovery Replication
**Given** MinIO primary deployment in Data Center 1  
**When** configuring disaster recovery  
**Then**:
- MinIO bucket replication configured to secondary Zambian data center
- Replication mode: Active-passive (primary to secondary only)
- RPO target: <1 hour (replication lag monitored)
- DR failover procedure tested quarterly
- Replication status dashboard shows replication lag and last successful sync

### AC6: Admin UI Archive Search
**Given** Compliance Officer needs to query archived audit data  
**When** using Admin portal archive search  
**Then**:
- Search interface allows date range selection
- Backend queries MinIO bucket metadata index
- Results show matching daily export files available for download
- Download triggers audit access log entry
- Search performance <5 seconds for 7-year date range query

### AC7: Retention Policy Enforcement
**Given** WORM retention period of 10 years configured  
**When** attempting to delete audit data before expiration  
**Then**:
- MinIO Object Lock prevents deletion (returns 403 Forbidden)
- Attempted deletion logged as security incident
- Retention expiration date displayed in Admin UI (informational only)
- After 10 years + 1 day, objects become deletable (but not automatically deleted)
- Annual review process determines if extended retention needed beyond 10 years

---

## Technical Implementation Details

### Architecture Reference

**PRD Sections**: Section 5.3 (Phase 3 Stories), Lines 996-1019  
**Architecture Sections**: Section 5.2 (Data Retention Strategy), Lines 1073-1121  
**ADR References**: ADR-010 (Audit Storage - MinIO WORM)  
**Requirements**: FR14, NFR17

### Technology Stack

- **Object Storage**: MinIO (self-hosted, S3-compatible)
- **Compression**: Gzip (System.IO.Compression.GzipStream)
- **Serialization**: System.Text.Json (JSONL format)
- **Scheduling**: Hangfire or Quartz.NET for daily export jobs
- **SDK**: MinIO.AspNet SDK for .NET

### MinIO Configuration

```yaml
# docker-compose.yml - MinIO deployment
version: '3.8'

services:
  minio:
    image: minio/minio:RELEASE.2024-01-01T00-00-00Z
    container_name: minio-audit
    ports:
      - "9000:9000"  # API
      - "9001:9001"  # Console
    environment:
      MINIO_ROOT_USER: ${MINIO_ROOT_USER}
      MINIO_ROOT_PASSWORD: ${MINIO_ROOT_PASSWORD}
      MINIO_SERVER_URL: https://minio.intellifin.local
      MINIO_BROWSER_REDIRECT_URL: https://minio-console.intellifin.local
    command: server /data --console-address ":9001"
    volumes:
      - minio-data:/data
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:9000/minio/health/live"]
      interval: 30s
      timeout: 20s
      retries: 3
    restart: unless-stopped

volumes:
  minio-data:
    driver: local
```

```bash
# minio-setup.sh - Bucket initialization script
#!/bin/bash

# Install mc (MinIO Client)
wget https://dl.min.io/client/mc/release/linux-amd64/mc
chmod +x mc
mv mc /usr/local/bin/

# Configure mc alias
mc alias set intellifin https://minio.intellifin.local ${MINIO_ROOT_USER} ${MINIO_ROOT_PASSWORD}

# Create bucket with versioning and object lock
mc mb --with-lock intellifin/audit-logs
mc version enable intellifin/audit-logs

# Set default retention (7 years = 2555 days)
mc retention set --default COMPLIANCE "3654d" intellifin/audit-logs

# Create access logs bucket
mc mb --with-lock intellifin/audit-access-logs
mc retention set --default COMPLIANCE "3654d" intellifin/audit-access-logs

# Enable bucket lifecycle policy (optional - for automatic deletion after retention)
cat > lifecycle-policy.json <<EOF
{
  "Rules": [
    {
"ID": "expire-after-10-years-plus-grace",
      "Status": "Enabled",
      "Expiration": {
"Days": 4018
      },
      "Filter": {
        "Prefix": ""
      }
    }
  ]
}
EOF
mc ilm import intellifin/audit-logs < lifecycle-policy.json

# Enable bucket notifications for access logging (optional)
mc admin config set intellifin logger_webhook:audit_access endpoint="https://admin-service.intellifin.local/api/admin/audit/minio-access"

echo "MinIO audit storage configured successfully"
```

### Database Schema

```sql
-- Admin Service database
CREATE TABLE AuditArchiveMetadata (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ArchiveId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    FileName NVARCHAR(500) NOT NULL,
    ObjectKey NVARCHAR(1000) NOT NULL,  -- MinIO object key
    ExportDate DATETIME2 NOT NULL,
    EventDateStart DATETIME2 NOT NULL,
    EventDateEnd DATETIME2 NOT NULL,
    EventCount INT NOT NULL,
    FileSize BIGINT NOT NULL,  -- Bytes
    CompressionRatio DECIMAL(5,2),
    ChainStartHash NVARCHAR(64),  -- First event hash in file
    ChainEndHash NVARCHAR(64),  -- Last event hash in file
    RetentionExpiryDate DATETIME2 NOT NULL,  -- 7 years from export
    StorageLocation NVARCHAR(100) DEFAULT 'PRIMARY',  -- PRIMARY or DR
    ReplicationStatus NVARCHAR(20) DEFAULT 'PENDING',  -- PENDING, REPLICATED, FAILED
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    INDEX IX_ExportDate (ExportDate DESC),
    INDEX IX_EventDateRange (EventDateStart, EventDateEnd),
    INDEX IX_ObjectKey (ObjectKey)
);
```

### Daily Export Service

```csharp
// AuditArchiveService.cs
public class AuditArchiveService : IAuditArchiveService
{
    private readonly AdminDbContext _db;
    private readonly IMinioClient _minioClient;
    private readonly ILogger<AuditArchiveService> _logger;
    private const string BucketName = "audit-logs";
    
    public async Task<ArchiveResult> ExportDailyAuditEventsAsync(DateTime exportDate)
    {
        _logger.LogInformation("Starting daily audit export for date: {Date}", exportDate.Date);
        
        var startOfDay = exportDate.Date;
        var endOfDay = startOfDay.AddDays(1).AddTicks(-1);
        
        // Query audit events for the day
        var events = await _db.AuditEvents
            .Where(e => e.Timestamp >= startOfDay && e.Timestamp <= endOfDay)
            .OrderBy(e => e.Timestamp)
            .ThenBy(e => e.Id)
            .ToListAsync();
        
        if (events.Count == 0)
        {
            _logger.LogInformation("No audit events to export for {Date}", exportDate.Date);
            return new ArchiveResult { Success = true, EventCount = 0 };
        }
        
        // Generate JSONL file
        var fileName = $"audit-events-{exportDate:yyyy-MM-dd}.jsonl.gz";
        var tempFilePath = Path.Combine(Path.GetTempPath(), fileName);
        
        using (var fileStream = File.Create(tempFilePath))
        using (var gzipStream = new GZipStream(fileStream, CompressionLevel.Optimal))
        using (var writer = new StreamWriter(gzipStream, Encoding.UTF8))
        {
            foreach (var evt in events)
            {
                var jsonLine = JsonSerializer.Serialize(new
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
                });
                
                await writer.WriteLineAsync(jsonLine);
            }
        }
        
        // Calculate file size and compression ratio
        var fileInfo = new FileInfo(tempFilePath);
        var uncompressedSize = events.Sum(e => Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(e)));
        var compressionRatio = (decimal)fileInfo.Length / uncompressedSize;
        
        // Upload to MinIO with WORM retention
        var objectKey = $"{exportDate:yyyy}/{exportDate:MM}/{fileName}";
        
        await _minioClient.PutObjectAsync(new PutObjectArgs()
            .WithBucket(BucketName)
            .WithObject(objectKey)
            .WithFileName(tempFilePath)
            .WithContentType("application/x-ndjson")
            .WithHeaders(new Dictionary<string, string>
            {
                ["x-amz-meta-export-date"] = exportDate.ToString("yyyy-MM-dd"),
                ["x-amz-meta-event-count"] = events.Count.ToString(),
                ["x-amz-meta-chain-start"] = events.First().CurrentEventHash,
                ["x-amz-meta-chain-end"] = events.Last().CurrentEventHash
            })
            .WithObjectLock(new ObjectLockArgs()
                .WithMode(RetentionMode.COMPLIANCE)
                .WithRetainUntilDate(exportDate.AddYears(7).AddDays(1))
            ));
        
        _logger.LogInformation(
            "Uploaded {FileName} to MinIO. Size: {Size} bytes, Events: {Count}",
            fileName, fileInfo.Length, events.Count);
        
        // Store metadata in database
        var metadata = new AuditArchiveMetadata
        {
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
RetentionExpiryDate = exportDate.AddYears(10).AddDays(1),
            StorageLocation = "PRIMARY",
            ReplicationStatus = "PENDING"
        };
        
        await _db.AuditArchiveMetadata.AddAsync(metadata);
        await _db.SaveChangesAsync();
        
        // Cleanup temp file
        File.Delete(tempFilePath);
        
        // Trigger replication to DR site
        await TriggerDRReplicationAsync(objectKey);
        
        // Mark old SQL Server events for archival cleanup (>90 days)
        var cleanupDate = DateTime.UtcNow.AddDays(-90);
        var eventsToArchive = await _db.AuditEvents
            .Where(e => e.Timestamp < cleanupDate && e.MigrationSource != "ARCHIVED")
            .ToListAsync();
        
        foreach (var evt in eventsToArchive)
        {
            evt.MigrationSource = "ARCHIVED";  // Soft delete
        }
        await _db.SaveChangesAsync();
        
        _logger.LogInformation(
            "Daily audit export completed. {EventCount} events exported, {CleanupCount} events marked archived",
            events.Count, eventsToArchive.Count);
        
        return new ArchiveResult
        {
            Success = true,
            EventCount = events.Count,
            FileName = fileName,
            ObjectKey = objectKey,
            FileSize = fileInfo.Length
        };
    }
    
    private async Task TriggerDRReplicationAsync(string objectKey)
    {
        // MinIO site replication handles this automatically if configured
        // This method can verify replication status
        try
        {
            var replicationStatus = await _minioClient.GetObjectReplicationAsync(
                new GetObjectReplicationArgs()
                    .WithBucket(BucketName)
                    .WithObject(objectKey));
            
            _logger.LogInformation("DR replication status for {ObjectKey}: {Status}", 
                objectKey, replicationStatus.Status);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check DR replication status for {ObjectKey}", objectKey);
        }
    }
}

// Background job for daily export
public class DailyAuditExportJob : IJob
{
    private readonly IAuditArchiveService _archiveService;
    private readonly ILogger<DailyAuditExportJob> _logger;
    
    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            // Export previous day's audit events
            var exportDate = DateTime.UtcNow.Date.AddDays(-1);
            
            _logger.LogInformation("Executing daily audit export job for {Date}", exportDate);
            
            var result = await _archiveService.ExportDailyAuditEventsAsync(exportDate);
            
            if (result.Success)
            {
                _logger.LogInformation(
                    "Daily audit export completed successfully. {EventCount} events exported to {FileName}",
                    result.EventCount, result.FileName);
            }
            else
            {
                _logger.LogError("Daily audit export failed for {Date}", exportDate);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during daily audit export job");
            throw;
        }
    }
}

// Startup.cs - Schedule job
services.AddQuartz(q =>
{
    var jobKey = new JobKey("DailyAuditExportJob");
    
    q.AddJob<DailyAuditExportJob>(opts => opts.WithIdentity(jobKey));
    
    q.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithIdentity("DailyAuditExportTrigger")
        .WithCronSchedule("0 0 0 * * ?")); // Daily at midnight UTC
});
```

### Archive Search API

```csharp
// AuditController.cs
[HttpGet("archive/search")]
[Authorize(Roles = "ComplianceOfficer,Auditor")]
public async Task<IActionResult> SearchArchive(
    [FromQuery] DateTime? startDate,
    [FromQuery] DateTime? endDate)
{
    var query = _db.AuditArchiveMetadata.AsQueryable();
    
    if (startDate.HasValue)
        query = query.Where(a => a.EventDateEnd >= startDate.Value);
    
    if (endDate.HasValue)
        query = query.Where(a => a.EventDateStart <= endDate.Value);
    
    var archives = await query
        .OrderByDescending(a => a.ExportDate)
        .Select(a => new
        {
            a.ArchiveId,
            a.FileName,
            a.ObjectKey,
            a.ExportDate,
            a.EventDateStart,
            a.EventDateEnd,
            a.EventCount,
            a.FileSize,
            a.RetentionExpiryDate,
            a.StorageLocation,
            a.ReplicationStatus
        })
        .ToListAsync();
    
    return Ok(new
    {
        archives,
        totalCount = archives.Count,
        totalEvents = archives.Sum(a => a.EventCount),
        totalSize = archives.Sum(a => a.FileSize)
    });
}

[HttpGet("archive/download/{archiveId}")]
[Authorize(Roles = "ComplianceOfficer,Auditor")]
public async Task<IActionResult> DownloadArchive(Guid archiveId)
{
    var archive = await _db.AuditArchiveMetadata
        .FirstOrDefaultAsync(a => a.ArchiveId == archiveId);
    
    if (archive == null)
        return NotFound();
    
    // Log access for compliance
    await _auditClient.LogEventAsync(
        "ARCHIVE_DOWNLOADED",
        "AuditArchive",
        archiveId.ToString(),
        new { archive.FileName, archive.EventCount });
    
    // Get presigned URL from MinIO (temporary download link)
    var presignedUrl = await _minioClient.PresignedGetObjectAsync(
        new PresignedGetObjectArgs()
            .WithBucket("audit-logs")
            .WithObject(archive.ObjectKey)
            .WithExpiry(3600)); // 1 hour expiry
    
    return Ok(new
    {
        downloadUrl = presignedUrl,
        fileName = archive.FileName,
        expiresIn = 3600
    });
}
```

---

## Integration Verification

### IV1: Hot Storage to Cold Storage Migration
**Verification Steps**:
1. Verify audit events >90 days marked as ARCHIVED in SQL Server
2. Confirm corresponding MinIO objects exist for archived date ranges
3. Test query: Old events retrievable via archive search

**Success Criteria**: No data loss during hot-to-cold migration.

### IV2: WORM Retention Enforcement
**Verification Steps**:
1. Attempt to delete recently uploaded audit archive from MinIO
2. Attempt to modify object metadata
3. Verify both operations blocked (403 Forbidden)

**Success Criteria**: MinIO Object Lock prevents deletion/modification.

### IV3: DR Replication
**Verification Steps**:
1. Upload audit archive to primary MinIO
2. Wait for replication interval
3. Verify object exists in secondary data center MinIO

**Success Criteria**: RPO <1 hour, object replicated with retention intact.

---

## Testing Strategy

### Unit Tests
1. **JSONL Export**: Test audit event serialization to JSONL format
2. **Compression**: Test gzip compression ratios
3. **Metadata Creation**: Test metadata record generation

### Integration Tests
1. **End-to-End Export**: Insert 1000 events → Run export → Verify MinIO object created
2. **Download Flow**: Search archive → Download → Verify file integrity
3. **Retention Test**: Upload object → Attempt delete → Verify blocked

### Performance Tests
- **Export Performance**: 100,000 events export completes in <5 minutes
- **Search Performance**: 10-year archive search <5 seconds
- **Download Speed**: 1GB archive download starts within 3 seconds

### Security Tests
1. **Access Control**: Unauthorized user cannot download archives
2. **WORM Bypass Attempt**: Even admin cannot delete WORM objects
3. **Access Logging**: Verify all archive access logged

---

## Risks and Mitigation

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| MinIO storage capacity exhaustion | High | Low | Monitor storage usage, capacity planning for 7 years, compression reduces size by ~70% |
| Export job failure causes audit data loss | High | Medium | Retry logic, export job monitoring with alerts, keep SQL data until verified in MinIO |
| DR replication lag exceeds 1 hour | Medium | Medium | Monitor replication status, alert on lag >30 minutes, dedicated replication bandwidth |
| WORM retention misconfiguration | High | Low | Pre-production testing, compliance review of retention settings, immutability prevents accidents |

---

## Definition of Done (DoD)

- [ ] MinIO bucket created with Object Lock COMPLIANCE mode
- [ ] Default 7-year retention policy configured and tested
- [ ] Daily export job scheduled and tested
- [ ] Audit archive metadata table populated
- [ ] Archive search API operational
- [ ] Archive download with access logging working
- [ ] DR replication configured and tested
- [ ] WORM retention enforcement verified (deletion blocked)
- [ ] SQL Server cleanup of archived events (>90 days) automated
- [ ] All integration verification criteria passed
- [ ] Documentation updated in `docs/domains/system-administration/minio-worm-storage.md`
- [ ] Compliance team trained on archive search and download
- [ ] Quarterly DR failover test scheduled

---

## Related Documentation

### PRD References
- **Full PRD**: `../system-administration-control-plane-prd.md` (Lines 996-1019)
- **Requirements**: FR14, NFR17

### Architecture References
- **Full Architecture**: `../system-administration-control-plane-architecture.md` (Section 5.2, Lines 1073-1121)
- **ADRs**: ADR-010 (Audit Storage - MinIO WORM)

---

## Notes for Development Team

### Pre-Implementation Checklist
- [ ] MinIO server deployed with sufficient storage (estimate 7 years retention)
- [ ] MinIO mc (client) installed for bucket management
- [ ] Network connectivity between Admin Service and MinIO verified
- [ ] DR secondary data center MinIO deployed
- [ ] Site replication configured between primary and secondary MinIO

### Post-Implementation Handoff
- Runbook created for MinIO capacity monitoring and scaling
- Compliance team trained on archive search and audit evidence retrieval
- First export job monitored to completion
- DR failover test scheduled (quarterly)

---

**Story Created**: 2025-10-11  
**Last Updated**: 2025-10-11  
**Next Story**: Story 1.17 - Global Correlation ID Propagation
