# Story 1.18: Offline CEO App Audit Merge Implementation

## Story Metadata

| Field | Value |
|-------|-------|
| **Story ID** | 1.18 |
| **Epic** | System Administration Control Plane Enhancement |
| **Phase** | Phase 3: Audit & Compliance |
| **Sprint** | Sprint 7 |
| **Story Points** | 10 |
| **Estimated Effort** | 7-10 days |
| **Priority** | P1 (Critical for CEO offline operations) |
| **Status** | 📋 Backlog |
| **Assigned To** | TBD |
| **Dependencies** | Story 1.15 (Tamper-evident chain), Story 1.14 (Centralized audit), CEO desktop app existing |
| **Blocks** | None |

---

## User Story

**As a** CEO,  
**I want** offline audit events from my desktop app merged into the central audit system,  
**so that** my offline loan approvals are included in compliance audit trails.

---

## Business Value

Offline audit merge enables business continuity for CEO operations while maintaining compliance:

- **Business Continuity**: CEO can approve loans offline during travel or connectivity issues
- **Compliance Coverage**: All CEO actions included in 7-year BoZ audit retention (no gaps)
- **Audit Integrity**: Offline events integrated into tamper-evident chain without breaking integrity
- **Regulatory Confidence**: Regulators can audit complete transaction history including offline operations
- **Operational Flexibility**: Supports remote operations without compromising audit requirements

This closes the audit gap for offline desktop application workflows.

---

## Acceptance Criteria

### AC1: CEO App Offline Audit Batching
**Given** CEO desktop app operating offline (no network connectivity)  
**When** CEO performs actions (loan approvals, client modifications)  
**Then**:
- Audit events logged to local SQLite database
- Events include all standard audit fields (Timestamp, Actor, Action, Entity)
- Events tagged with `IsOffline = TRUE` flag
- Local queue shows pending sync count in UI
- No audit failures block business operations

### AC2: Sync Endpoint for Batch Upload
**Given** CEO desktop app reconnects to network  
**When** sync operation initiated  
**Then**:
- Admin Service exposes POST `/api/admin/audit/merge-offline` endpoint
- Endpoint accepts batch of offline audit events (max 10,000 per batch)
- Authentication via CEO's JWT token (validates CEO role)
- Upload includes device ID and offline session metadata
- Response returns merge status (success/failure per event)

### AC3: Conflict Resolution - Duplicate Detection
**Given** Offline events being merged  
**When** checking for duplicates  
**Then**:
- Duplicate detection by: `CorrelationId + Timestamp + Actor + Action + EntityId`
- Exact duplicates skipped with log entry (idempotent merge)
- Similar events within 5-second window flagged for manual review
- Duplicate count returned in merge response
- CEO notified of skipped duplicates in sync UI

### AC4: Chronological Chain Insertion
**Given** Offline events with timestamps between existing online events  
**When** merging into tamper-evident chain  
**Then**:
- Events inserted at chronologically correct position (not appended to end)
- Events after insertion point have `PreviousEventHash` recalculated
- Chain re-hashing performed incrementally (not full chain rebuild)
- Original event hashes preserved (new `OfflineMergeHash` field for traceability)
- Chain integrity verification passes post-merge

### AC5: Chain Re-Hashing Algorithm
**Given** Offline events inserted mid-chain  
**When** recalculating hashes  
**Then**:
- Only events **after** insertion point re-hashed (optimization)
- Re-hashing parallelized for performance (batch processing)
- Original pre-merge hashes stored in audit history table
- Re-hash operation logged with event count and duration
- Chain verification confirms integrity post-merge

### AC6: Merge Audit Trail
**Given** Offline merge operation completed  
**When** recording merge metadata  
**Then**:
- Merge operation logged: timestamp, CEO user, device ID, event count
- Conflicts detected logged (duplicates, re-hash count)
- Merge duration logged (performance tracking)
- Separate `OfflineMergeHistory` table stores merge operations
- Failed merges logged with error details for debugging

### AC7: CEO App Sync Status UI
**Given** CEO desktop app with pending offline events  
**When** viewing sync status  
**Then**:
- UI shows pending event count with "Sync Now" button
- Last successful sync timestamp displayed
- Sync progress bar during upload (event N of M)
- Sync errors displayed with retry option
- Successfully synced events removed from local SQLite database

### AC8: Performance Target
**Given** 1000 offline events to merge  
**When** merge operation executes  
**Then**:
- Complete merge (upload + chain insertion + re-hash) <30 seconds
- Chain re-hashing optimized (only affected events)
- No blocking of online audit ingestion during merge
- Background re-hashing doesn't impact API response times

---

## Technical Implementation Details

### Architecture Reference

**PRD Sections**: Section 5.3 (Phase 3 Stories), Lines 1048-1071  
**Architecture Sections**: Section 4.1.2 (Audit Schema), Lines 398-503  
**Requirements**: FR16, CR5

### Technology Stack

- **CEO Desktop App**: .NET MAUI (existing)
- **Local Storage**: SQLite
- **Sync Protocol**: HTTP/JSON (Admin Service REST API)
- **Chain Re-Hashing**: Parallel processing (Task Parallel Library)

### CEO Desktop App - Local SQLite Schema

```sql
-- CEO desktop app SQLite database
CREATE TABLE OfflineAuditEvents (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    EventId TEXT NOT NULL UNIQUE,
    Timestamp TEXT NOT NULL,  -- ISO 8601 format
    Actor TEXT NOT NULL,
    Action TEXT NOT NULL,
    EntityType TEXT,
    EntityId TEXT,
    EventData TEXT,  -- JSON
    DeviceId TEXT NOT NULL,
    OfflineSessionId TEXT NOT NULL,
    IsSynced INTEGER DEFAULT 0,  -- Boolean: 0 = pending, 1 = synced
    SyncAttempts INTEGER DEFAULT 0,
    LastSyncError TEXT,
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_synced ON OfflineAuditEvents(IsSynced);
CREATE INDEX idx_timestamp ON OfflineAuditEvents(Timestamp);
```

### Admin Service - Offline Merge Schema

```sql
-- Admin Service database
ALTER TABLE AuditEvents
ADD IsOfflineEvent BIT DEFAULT 0,
    OfflineDeviceId NVARCHAR(100) NULL,
    OfflineSessionId NVARCHAR(100) NULL,
    OfflineMergeId UNIQUEIDENTIFIER NULL,
    OriginalHash NVARCHAR(64) NULL;  -- Hash before re-hashing

CREATE TABLE OfflineMergeHistory (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    MergeId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    MergeTimestamp DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UserId NVARCHAR(100) NOT NULL,
    DeviceId NVARCHAR(100) NOT NULL,
    OfflineSessionId NVARCHAR(100) NOT NULL,
    EventsReceived INT NOT NULL,
    EventsMerged INT NOT NULL,
    DuplicatesSkipped INT NOT NULL,
    EventsReHashed INT NOT NULL,
    MergeDurationMs INT NOT NULL,
    Status NVARCHAR(20) NOT NULL,  -- SUCCESS, PARTIAL_SUCCESS, FAILED
    ErrorDetails NVARCHAR(MAX),
    INDEX IX_MergeTimestamp (MergeTimestamp DESC),
    INDEX IX_UserId (UserId)
);
```

### Offline Merge Service

```csharp
// OfflineAuditMergeService.cs
public class OfflineAuditMergeService : IOfflineAuditMergeService
{
    private readonly AdminDbContext _db;
    private readonly IAuditHashService _hashService;
    private readonly ILogger<OfflineAuditMergeService> _logger;
    
    public async Task<MergeResult> MergeOfflineEventsAsync(
        OfflineMergeRequest request, 
        string userId)
    {
        var mergeId = Guid.NewGuid();
        var stopwatch = Stopwatch.StartNew();
        
        _logger.LogInformation(
            "Starting offline audit merge. MergeId: {MergeId}, Events: {Count}, User: {UserId}",
            mergeId, request.Events.Count, userId);
        
        var result = new MergeResult
        {
            MergeId = mergeId,
            EventsReceived = request.Events.Count
        };
        
        try
        {
            // Step 1: Duplicate detection
            var newEvents = await DetectDuplicatesAsync(request.Events, result);
            
            if (newEvents.Count == 0)
            {
                _logger.LogInformation("All events are duplicates. Skipping merge.");
                await LogMergeHistoryAsync(request, result, userId, stopwatch.ElapsedMilliseconds);
                return result;
            }
            
            // Step 2: Sort events chronologically
            var sortedEvents = newEvents.OrderBy(e => e.Timestamp).ToList();
            
            // Step 3: Insert events and identify chain break points
            var chainBreakPoints = new List<long>();
            
            foreach (var offlineEvent in sortedEvents)
            {
                var auditEvent = new AuditEvent
                {
                    EventId = Guid.Parse(offlineEvent.EventId),
                    Timestamp = DateTime.Parse(offlineEvent.Timestamp),
                    Actor = offlineEvent.Actor,
                    Action = offlineEvent.Action,
                    EntityType = offlineEvent.EntityType,
                    EntityId = offlineEvent.EntityId,
                    EventData = offlineEvent.EventData,
                    IsOfflineEvent = true,
                    OfflineDeviceId = request.DeviceId,
                    OfflineSessionId = request.OfflineSessionId,
                    OfflineMergeId = mergeId,
                    IntegrityStatus = "PENDING_REHASH"
                };
                
                // Find insertion point (first event after this timestamp)
                var nextEvent = await _db.AuditEvents
                    .Where(e => e.Timestamp > auditEvent.Timestamp)
                    .OrderBy(e => e.Timestamp)
                    .FirstOrDefaultAsync();
                
                if (nextEvent != null)
                {
                    chainBreakPoints.Add(nextEvent.Id);
                }
                
                await _db.AuditEvents.AddAsync(auditEvent);
                result.EventsMerged++;
            }
            
            await _db.SaveChangesAsync();
            
            // Step 4: Re-hash affected portions of chain
            if (chainBreakPoints.Any())
            {
                await ReHashChainSegmentsAsync(chainBreakPoints, result);
            }
            
            // Step 5: Log merge history
            await LogMergeHistoryAsync(request, result, userId, stopwatch.ElapsedMilliseconds);
            
            result.Status = "SUCCESS";
            
            _logger.LogInformation(
                "Offline audit merge completed successfully. MergeId: {MergeId}, Merged: {Merged}, Duplicates: {Duplicates}, ReHashed: {ReHashed}, Duration: {Duration}ms",
                mergeId, result.EventsMerged, result.DuplicatesSkipped, result.EventsReHashed, stopwatch.ElapsedMilliseconds);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during offline audit merge. MergeId: {MergeId}", mergeId);
            
            result.Status = "FAILED";
            result.ErrorDetails = ex.Message;
            
            await LogMergeHistoryAsync(request, result, userId, stopwatch.ElapsedMilliseconds);
            
            throw;
        }
    }
    
    private async Task<List<OfflineAuditEvent>> DetectDuplicatesAsync(
        List<OfflineAuditEvent> events, 
        MergeResult result)
    {
        var newEvents = new List<OfflineAuditEvent>();
        
        foreach (var evt in events)
        {
            // Check for exact duplicate by CorrelationId + Timestamp + Actor + Action + EntityId
            var isDuplicate = await _db.AuditEvents.AnyAsync(e =>
                e.CorrelationId == evt.CorrelationId &&
                e.Timestamp == DateTime.Parse(evt.Timestamp) &&
                e.Actor == evt.Actor &&
                e.Action == evt.Action &&
                e.EntityId == evt.EntityId);
            
            if (isDuplicate)
            {
                result.DuplicatesSkipped++;
                _logger.LogDebug(
                    "Skipping duplicate event: {EventId}, Action: {Action}, Timestamp: {Timestamp}",
                    evt.EventId, evt.Action, evt.Timestamp);
            }
            else
            {
                newEvents.Add(evt);
            }
        }
        
        return newEvents;
    }
    
    private async Task ReHashChainSegmentsAsync(List<long> breakPoints, MergeResult result)
    {
        _logger.LogInformation(
            "Re-hashing chain segments. Break points: {Count}",
            breakPoints.Count);
        
        // Process each break point
        foreach (var breakPointId in breakPoints.Distinct().OrderBy(id => id))
        {
            // Get events from break point to end (or next break point)
            var eventsToReHash = await _db.AuditEvents
                .Where(e => e.Id >= breakPointId)
                .OrderBy(e => e.Timestamp)
                .ThenBy(e => e.Id)
                .Take(10000)  // Process in chunks of 10k
                .ToListAsync();
            
            if (eventsToReHash.Count == 0)
                continue;
            
            // Re-calculate hashes
            string? previousHash = null;
            
            // Get the hash of the event before the break point
            var eventBeforeBreak = await _db.AuditEvents
                .Where(e => e.Id < breakPointId)
                .OrderByDescending(e => e.Id)
                .FirstOrDefaultAsync();
            
            if (eventBeforeBreak != null)
            {
                previousHash = eventBeforeBreak.CurrentEventHash;
            }
            
            foreach (var evt in eventsToReHash)
            {
                // Store original hash before re-hashing
                if (string.IsNullOrEmpty(evt.OriginalHash))
                {
                    evt.OriginalHash = evt.CurrentEventHash;
                }
                
                // Recalculate hash
                evt.PreviousEventHash = previousHash;
                evt.CurrentEventHash = _hashService.CalculateHash(evt, previousHash);
                evt.IntegrityStatus = "REHASHED";
                
                previousHash = evt.CurrentEventHash;
                result.EventsReHashed++;
            }
            
            await _db.SaveChangesAsync();
        }
        
        _logger.LogInformation("Chain re-hashing completed. Events re-hashed: {Count}", result.EventsReHashed);
    }
    
    private async Task LogMergeHistoryAsync(
        OfflineMergeRequest request, 
        MergeResult result, 
        string userId, 
        long durationMs)
    {
        var history = new OfflineMergeHistory
        {
            MergeId = result.MergeId,
            UserId = userId,
            DeviceId = request.DeviceId,
            OfflineSessionId = request.OfflineSessionId,
            EventsReceived = result.EventsReceived,
            EventsMerged = result.EventsMerged,
            DuplicatesSkipped = result.DuplicatesSkipped,
            EventsReHashed = result.EventsReHashed,
            MergeDurationMs = (int)durationMs,
            Status = result.Status,
            ErrorDetails = result.ErrorDetails
        };
        
        await _db.OfflineMergeHistory.AddAsync(history);
        await _db.SaveChangesAsync();
    }
}
```

### API Endpoint

```csharp
// AuditController.cs
[HttpPost("merge-offline")]
[Authorize(Roles = "CEO")]
public async Task<IActionResult> MergeOfflineEvents([FromBody] OfflineMergeRequest request)
{
    if (!ModelState.IsValid)
        return BadRequest(ModelState);
    
    if (request.Events.Count > 10000)
        return BadRequest("Batch size cannot exceed 10,000 events");
    
    var userId = User.Identity?.Name ?? "Unknown";
    
    _logger.LogInformation(
        "Offline merge request received from {UserId}. Device: {DeviceId}, Events: {Count}",
        userId, request.DeviceId, request.Events.Count);
    
    try
    {
        var result = await _offlineMergeService.MergeOfflineEventsAsync(request, userId);
        
        return Ok(new
        {
            mergeId = result.MergeId,
            status = result.Status,
            eventsReceived = result.EventsReceived,
            eventsMerged = result.EventsMerged,
            duplicatesSkipped = result.DuplicatesSkipped,
            eventsReHashed = result.EventsReHashed,
            message = $"Successfully merged {result.EventsMerged} events. Skipped {result.DuplicatesSkipped} duplicates."
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error during offline merge for user {UserId}", userId);
        return StatusCode(500, new { error = "Merge failed", details = ex.Message });
    }
}

public class OfflineMergeRequest
{
    public string DeviceId { get; set; } = string.Empty;
    public string OfflineSessionId { get; set; } = string.Empty;
    public List<OfflineAuditEvent> Events { get; set; } = new();
}

public class OfflineAuditEvent
{
    public string EventId { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? CorrelationId { get; set; }
    public string? EventData { get; set; }
}
```

### CEO Desktop App - Sync Service

```csharp
// CEO Desktop App - SyncService.cs
public class AuditSyncService
{
    private readonly SQLiteConnection _localDb;
    private readonly HttpClient _httpClient;
    private readonly ILogger<AuditSyncService> _logger;
    
    public async Task<SyncResult> SyncOfflineEventsAsync()
    {
        var pendingEvents = _localDb.Table<OfflineAuditEvent>()
            .Where(e => e.IsSynced == 0)
            .OrderBy(e => e.Timestamp)
            .Take(10000)  // Sync in batches of 10k
            .ToList();
        
        if (pendingEvents.Count == 0)
        {
            _logger.LogInformation("No pending events to sync");
            return new SyncResult { Success = true, EventsSynced = 0 };
        }
        
        _logger.LogInformation("Syncing {Count} offline audit events", pendingEvents.Count);
        
        var request = new
        {
            DeviceId = GetDeviceId(),
            OfflineSessionId = Guid.NewGuid().ToString(),
            Events = pendingEvents.Select(e => new
            {
                e.EventId,
                e.Timestamp,
                e.Actor,
                e.Action,
                e.EntityType,
                e.EntityId,
                CorrelationId = e.EventId,  // Use EventId as correlation for offline events
                e.EventData
            }).ToList()
        };
        
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/api/admin/audit/merge-offline", 
                request);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<MergeResponse>();
                
                // Mark synced events as complete
                foreach (var evt in pendingEvents)
                {
                    evt.IsSynced = 1;
                    _localDb.Update(evt);
                }
                
                _logger.LogInformation(
                    "Sync completed successfully. Merged: {Merged}, Duplicates: {Duplicates}",
                    result.EventsMerged, result.DuplicatesSkipped);
                
                return new SyncResult
                {
                    Success = true,
                    EventsSynced = result.EventsMerged,
                    DuplicatesSkipped = result.DuplicatesSkipped
                };
            }
            else
            {
                _logger.LogError("Sync failed with status {StatusCode}", response.StatusCode);
                
                // Increment sync attempts
                foreach (var evt in pendingEvents)
                {
                    evt.SyncAttempts++;
                    evt.LastSyncError = $"HTTP {response.StatusCode}";
                    _localDb.Update(evt);
                }
                
                return new SyncResult
                {
                    Success = false,
                    ErrorMessage = $"Server returned {response.StatusCode}"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during sync");
            
            foreach (var evt in pendingEvents)
            {
                evt.SyncAttempts++;
                evt.LastSyncError = ex.Message;
                _localDb.Update(evt);
            }
            
            return new SyncResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
}
```

---

## Integration Verification

### IV1: Existing CEO Offline Workflows Unaffected
**Verification Steps**:
1. Test CEO app offline loan origination (existing functionality)
2. Verify loan approval workflow works offline
3. Confirm audit merge is additive (doesn't break existing features)

**Success Criteria**: All existing CEO offline operations functional.

### IV2: Chain Integrity Post-Merge
**Verification Steps**:
1. Insert 100 online audit events
2. Merge 50 offline events with timestamps interspersed
3. Run chain integrity verification (Story 1.15)
4. Verify chain passes validation

**Success Criteria**: No chain breaks introduced by offline merge.

### IV3: Performance Target Met
**Verification Steps**:
1. Prepare 1000 offline events
2. Measure merge operation duration
3. Verify <30 seconds total time

**Success Criteria**: 1000-event merge completes in <30 seconds.

---

## Testing Strategy

### Unit Tests
1. **Duplicate Detection**: Test various duplicate scenarios
2. **Hash Recalculation**: Test chain re-hashing logic
3. **Chronological Insertion**: Test event ordering

### Integration Tests
1. **End-to-End Merge**: Offline events → Sync → Verify in database → Verify chain integrity
2. **Conflict Resolution**: Duplicate events → Verify skipped correctly
3. **Chain Re-Hash**: Insert mid-chain → Verify subsequent events re-hashed

### Performance Tests
- **1000 Event Merge**: <30 seconds
- **10000 Event Merge**: <5 minutes
- **Concurrent Merge**: Multiple devices syncing simultaneously (no blocking)

### Edge Case Tests
1. **Network Interruption During Sync**: Resume capability
2. **Very Old Offline Events**: Merge events from 6 months ago
3. **Massive Offline Batch**: 10,000 events at once

---

## Risks and Mitigation

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| Chain re-hashing breaks integrity | High | Low | Comprehensive testing, backup before merge, rollback capability |
| Large offline batches overwhelm server | Medium | Medium | Batch size limits (10k), throttling, async processing |
| Clock drift causes timestamp issues | Low | Medium | Accept events with timestamp warnings, manual review for extreme drift |
| Sync failures leave CEO app in limbo | Medium | Low | Retry logic, local persistence until confirmed, manual sync UI |

---

## Definition of Done (DoD)

- [ ] CEO app local SQLite audit batching implemented
- [ ] Admin Service offline merge endpoint operational
- [ ] Duplicate detection working (idempotent merges)
- [ ] Chronological chain insertion implemented
- [ ] Chain re-hashing algorithm tested and optimized
- [ ] Merge audit trail logged in OfflineMergeHistory table
- [ ] CEO app sync status UI showing pending events
- [ ] Performance target met (1000 events <30 seconds)
- [ ] All integration verification criteria passed
- [ ] Chain integrity verification passes post-merge
- [ ] Documentation updated in `docs/domains/system-administration/offline-audit-merge.md`
- [ ] CEO trained on sync workflow
- [ ] Runbook created for merge troubleshooting

---

## Related Documentation

### PRD References
- **Full PRD**: `../system-administration-control-plane-prd.md` (Lines 1048-1071)
- **Requirements**: FR16, CR5

### Architecture References
- **Full Architecture**: `../system-administration-control-plane-architecture.md` (Section 4.1.2, Lines 398-503)

---

## Notes for Development Team

### Pre-Implementation Checklist
- [ ] Story 1.15 (Tamper-evident chain) completed
- [ ] Story 1.14 (Centralized audit) completed
- [ ] CEO desktop app codebase accessible
- [ ] Local SQLite database schema designed
- [ ] Chain re-hashing algorithm reviewed with architects

### Post-Implementation Handoff
- CEO trained on sync workflow and troubleshooting
- Operations team has runbook for merge issues
- Monitoring dashboard shows merge operations and failures
- Backup/restore procedure tested for audit database

---

**Story Created**: 2025-10-11  
**Last Updated**: 2025-10-11  
**Phase 3 Complete**: Ready for Phase 4 - Governance & Workflows
