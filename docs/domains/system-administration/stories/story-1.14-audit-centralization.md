# Story 1.14: Audit Event Centralization in Admin Service

## Story Metadata

| Field | Value |
|-------|-------|
| **Story ID** | 1.14 |
| **Epic** | System Administration Control Plane Enhancement |
| **Phase** | Phase 3: Audit & Compliance |
| **Sprint** | Sprint 5 |
| **Story Points** | 10 |
| **Estimated Effort** | 7-10 days |
| **Priority** | P0 (Critical for compliance) |
| **Status** | 📋 Backlog |
| **Assigned To** | TBD |
| **Dependencies** | Story 1.4 (Admin Service), existing audit implementation in FinancialService |
| **Blocks** | Stories 1.15, 1.16, 1.17, 1.18 |

---

## User Story

**As a** Compliance Officer,  
**I want** all audit events collected centrally in Admin Service,  
**so that** I have a unified audit trail for regulatory reporting and compliance.

---

## Business Value

Centralizing audit events in the Admin Service addresses critical architectural and compliance gaps:

- **Regulatory Compliance**: Unified audit trail for Bank of Zambia 10-year retention requirements
- **Architectural Correctness**: Moves audit from FinancialService (misplaced domain) to System Administration
- **Compliance Reporting**: Single source of truth for audit queries and BoZ reports
- **Audit Integrity**: Foundation for tamper-evident chains (Story 1.15) and WORM storage (Story 1.16)
- **Operational Efficiency**: Eliminates scattered audit logs across multiple services

This story is the cornerstone of the compliance architecture enhancement.

---

## Acceptance Criteria

### AC1: Audit Event Schema Defined
**Given** Admin Service database is operational  
**When** defining the audit event schema  
**Then**:
- `AuditEvent` entity created with all required fields
- Schema supports tamper-evident chain (PreviousEventHash, CurrentEventHash placeholders)
- Correlation ID field for distributed tracing integration
- Indexed fields for efficient querying (Timestamp, Actor, CorrelationId)
- Event data stored as JSON for flexibility

### AC2: Audit Ingestion API Implemented
**Given** Admin Service exposes audit endpoints  
**When** services send audit events  
**Then**:
- POST `/api/admin/audit/events` endpoint accepts audit event payloads
- Bulk ingestion endpoint POST `/api/admin/audit/events/batch` for batch submissions
- Input validation ensures required fields present (Timestamp, Actor, Action)
- Events persisted to Admin Service database within 100ms p95 (NFR4)
- Ingestion failures logged and retried

### AC3: All Services Updated to Use Admin Service Audit
**Given** All IntelliFin microservices have audit logging  
**When** refactoring audit calls  
**Then**:
- IdentityService audit calls redirected to Admin Service
- LoanOrigination audit calls redirected to Admin Service
- FinancialService audit calls redirected to Admin Service
- ClientManagement audit calls redirected to Admin Service
- Collections audit calls redirected to Admin Service
- Communications audit calls redirected to Admin Service
- All services use `IAuditService` abstraction pointing to Admin Service HTTP client

### AC4: RabbitMQ Async Audit Option
**Given** High-throughput audit requirements (10,000 events/sec per NFR4)  
**When** implementing async audit ingestion  
**Then**:
- RabbitMQ exchange `audit.events` created with durable settings
- Services publish audit events to `audit.events` exchange
- Admin Service consumes from `audit.events` queue with batch processing
- Message acknowledgment only after successful DB persistence
- Dead-letter queue configured for failed audit events

### AC5: Existing Audit Data Migration
**Given** Audit events exist in FinancialService database  
**When** ETL migration script executes  
**Then**:
- All existing audit events extracted from FinancialService.AuditLogs table
- Events migrated to Admin Service database with preserved timestamps
- Migrated events flagged with `MigrationSource = 'FinancialService'`
- Migration validation report confirms record count matches source
- Historical audit queries continue working via unified API

### AC6: Audit Query API Implemented
**Given** Centralized audit events in Admin Service  
**When** querying audit trail  
**Then**:
- GET `/api/admin/audit/events` with filtering: date range, actor, action, entity type, entity ID, correlation ID
- Pagination support (page size 100 default, max 1000)
- Response includes total count and page metadata
- Query performance <1 second for 30-day range with filters
- Export endpoint GET `/api/admin/audit/events/export` for CSV/Excel compliance reports

### AC7: Audit Event Batching for Performance
**Given** Burst traffic of 10,000 events/sec (NFR4)  
**When** ingesting audit events  
**Then**:
- In-memory buffer batches events (max 1000 events or 5 seconds, whichever first)
- Batch insert to database using SQL Server Table-Valued Parameters (TVP)
- Background worker flushes buffer every 5 seconds
- Buffer overflow protection: blocks ingestion if buffer >100,000 events (alerts triggered)

---

## Technical Implementation Details

### Architecture Reference

**PRD Sections**: Section 5.3 (Phase 3 Stories), Lines 944-967  
**Architecture Sections**: Section 4.1.2 (Admin Service Database Schema), Lines 398-503  
**Requirements**: FR12, NFR4

### Technology Stack

- **Database**: SQL Server 2022 (Admin Service database)
- **Messaging**: RabbitMQ (optional async audit ingestion)
- **Serialization**: System.Text.Json
- **HTTP Client**: Refit or HttpClient for service-to-Admin-Service calls
- **Background Processing**: .NET Hosted Service for batch flushing

### Database Schema

```sql
-- Admin Service Database
CREATE TABLE AuditEvents (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    EventId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() UNIQUE,
    Timestamp DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    Actor NVARCHAR(100) NOT NULL,  -- Username or ServiceName
    Action NVARCHAR(50) NOT NULL,  -- Created, Updated, Deleted, Approved, etc.
    EntityType NVARCHAR(100),  -- LoanApplication, Client, User, Role, etc.
    EntityId NVARCHAR(100),  -- ID of affected entity
    CorrelationId NVARCHAR(100),  -- W3C Trace Context traceparent
    IpAddress NVARCHAR(45),  -- IPv4 or IPv6
    UserAgent NVARCHAR(500),
    EventData NVARCHAR(MAX),  -- JSON payload with old/new values
    MigrationSource NVARCHAR(50),  -- NULL or 'FinancialService' for migrated events
    PreviousEventHash NVARCHAR(64),  -- For Story 1.15 tamper-evident chain
    CurrentEventHash NVARCHAR(64),  -- For Story 1.15 tamper-evident chain
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    INDEX IX_Timestamp (Timestamp DESC),
    INDEX IX_Actor (Actor),
    INDEX IX_Action (Action),
    INDEX IX_EntityType_EntityId (EntityType, EntityId),
    INDEX IX_CorrelationId (CorrelationId),
    INDEX IX_EventId (EventId)
);

-- Table-Valued Parameter type for batch insert
CREATE TYPE AuditEventTableType AS TABLE (
    EventId UNIQUEIDENTIFIER,
    Timestamp DATETIME2,
    Actor NVARCHAR(100),
    Action NVARCHAR(50),
    EntityType NVARCHAR(100),
    EntityId NVARCHAR(100),
    CorrelationId NVARCHAR(100),
    IpAddress NVARCHAR(45),
    UserAgent NVARCHAR(500),
    EventData NVARCHAR(MAX)
);

-- Stored procedure for batch insert
CREATE PROCEDURE sp_InsertAuditEventsBatch
    @Events AuditEventTableType READONLY
AS
BEGIN
    SET NOCOUNT ON;
    
    INSERT INTO AuditEvents (
        EventId, Timestamp, Actor, Action, EntityType, EntityId,
        CorrelationId, IpAddress, UserAgent, EventData
    )
    SELECT 
        EventId, Timestamp, Actor, Action, EntityType, EntityId,
        CorrelationId, IpAddress, UserAgent, EventData
    FROM @Events;
    
    SELECT @@ROWCOUNT AS InsertedCount;
END;
```

### API Endpoints

#### Admin Service Audit API

```csharp
// POST /api/admin/audit/events
[HttpPost("events")]
public async Task<IActionResult> CreateAuditEvent([FromBody] AuditEventDto auditEvent)
{
    if (!ModelState.IsValid)
        return BadRequest(ModelState);
    
    var entity = _mapper.Map<AuditEvent>(auditEvent);
    await _auditService.LogEventAsync(entity);
    
    return Accepted(new { eventId = entity.EventId });
}

// POST /api/admin/audit/events/batch
[HttpPost("events/batch")]
public async Task<IActionResult> CreateAuditEventsBatch([FromBody] List<AuditEventDto> auditEvents)
{
    if (auditEvents.Count > 1000)
        return BadRequest("Batch size cannot exceed 1000 events");
    
    var entities = _mapper.Map<List<AuditEvent>>(auditEvents);
    var insertedCount = await _auditService.LogEventsBatchAsync(entities);
    
    return Accepted(new { insertedCount });
}

// GET /api/admin/audit/events
[HttpGet("events")]
public async Task<IActionResult> GetAuditEvents(
    [FromQuery] DateTime? startDate,
    [FromQuery] DateTime? endDate,
    [FromQuery] string? actor,
    [FromQuery] string? action,
    [FromQuery] string? entityType,
    [FromQuery] string? entityId,
    [FromQuery] string? correlationId,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 100)
{
    if (pageSize > 1000)
        return BadRequest("Page size cannot exceed 1000");
    
    var filter = new AuditEventFilter
    {
        StartDate = startDate ?? DateTime.UtcNow.AddDays(-30),
        EndDate = endDate ?? DateTime.UtcNow,
        Actor = actor,
        Action = action,
        EntityType = entityType,
        EntityId = entityId,
        CorrelationId = correlationId,
        Page = page,
        PageSize = pageSize
    };
    
    var result = await _auditService.GetAuditEventsAsync(filter);
    
    return Ok(new
    {
        data = result.Events,
        pagination = new
        {
            currentPage = page,
            pageSize,
            totalCount = result.TotalCount,
            totalPages = (int)Math.Ceiling(result.TotalCount / (double)pageSize)
        }
    });
}

// GET /api/admin/audit/events/export
[HttpGet("events/export")]
public async Task<IActionResult> ExportAuditEvents(
    [FromQuery] DateTime? startDate,
    [FromQuery] DateTime? endDate,
    [FromQuery] string format = "csv")
{
    var filter = new AuditEventFilter
    {
        StartDate = startDate ?? DateTime.UtcNow.AddDays(-30),
        EndDate = endDate ?? DateTime.UtcNow
    };
    
    var events = await _auditService.GetAllAuditEventsAsync(filter);
    
    if (format.ToLower() == "csv")
    {
        var csv = _csvExporter.Export(events);
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", $"audit-events-{DateTime.UtcNow:yyyyMMdd}.csv");
    }
    
    return BadRequest("Unsupported format. Use 'csv'.");
}
```

### Service Implementation

```csharp
// IAuditService.cs
public interface IAuditService
{
    Task LogEventAsync(AuditEvent auditEvent);
    Task LogEventsBatchAsync(List<AuditEvent> auditEvents);
    Task<AuditEventResult> GetAuditEventsAsync(AuditEventFilter filter);
    Task<List<AuditEvent>> GetAllAuditEventsAsync(AuditEventFilter filter);
}

// AuditService.cs
public class AuditService : IAuditService
{
    private readonly AdminDbContext _db;
    private readonly ILogger<AuditService> _logger;
    private readonly ConcurrentQueue<AuditEvent> _eventBuffer;
    private readonly SemaphoreSlim _batchLock;
    
    public AuditService(AdminDbContext db, ILogger<AuditService> logger)
    {
        _db = db;
        _logger = logger;
        _eventBuffer = new ConcurrentQueue<AuditEvent>();
        _batchLock = new SemaphoreSlim(1, 1);
    }
    
    public async Task LogEventAsync(AuditEvent auditEvent)
    {
        // Add to buffer for batch processing
        _eventBuffer.Enqueue(auditEvent);
        
        // Check buffer size - flush if needed
        if (_eventBuffer.Count >= 1000)
        {
            await FlushBufferAsync();
        }
    }
    
    public async Task LogEventsBatchAsync(List<AuditEvent> auditEvents)
    {
        using var connection = _db.Database.GetDbConnection();
        await connection.OpenAsync();
        
        using var command = connection.CreateCommand();
        command.CommandText = "sp_InsertAuditEventsBatch";
        command.CommandType = CommandType.StoredProcedure;
        
        var tvpParam = command.CreateParameter();
        tvpParam.ParameterName = "@Events";
        tvpParam.SqlDbType = SqlDbType.Structured;
        tvpParam.TypeName = "AuditEventTableType";
        tvpParam.Value = CreateDataTable(auditEvents);
        command.Parameters.Add(tvpParam);
        
        var insertedCount = await command.ExecuteScalarAsync();
        _logger.LogInformation("Inserted {Count} audit events in batch", insertedCount);
    }
    
    private async Task FlushBufferAsync()
    {
        await _batchLock.WaitAsync();
        try
        {
            var batch = new List<AuditEvent>();
            while (_eventBuffer.TryDequeue(out var evt) && batch.Count < 1000)
            {
                batch.Add(evt);
            }
            
            if (batch.Count > 0)
            {
                await LogEventsBatchAsync(batch);
            }
        }
        finally
        {
            _batchLock.Release();
        }
    }
    
    public async Task<AuditEventResult> GetAuditEventsAsync(AuditEventFilter filter)
    {
        var query = _db.AuditEvents.AsQueryable();
        
        // Apply filters
        query = query.Where(e => e.Timestamp >= filter.StartDate && e.Timestamp <= filter.EndDate);
        
        if (!string.IsNullOrEmpty(filter.Actor))
            query = query.Where(e => e.Actor == filter.Actor);
        
        if (!string.IsNullOrEmpty(filter.Action))
            query = query.Where(e => e.Action == filter.Action);
        
        if (!string.IsNullOrEmpty(filter.EntityType))
            query = query.Where(e => e.EntityType == filter.EntityType);
        
        if (!string.IsNullOrEmpty(filter.EntityId))
            query = query.Where(e => e.EntityId == filter.EntityId);
        
        if (!string.IsNullOrEmpty(filter.CorrelationId))
            query = query.Where(e => e.CorrelationId == filter.CorrelationId);
        
        var totalCount = await query.CountAsync();
        
        var events = await query
            .OrderByDescending(e => e.Timestamp)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();
        
        return new AuditEventResult
        {
            Events = events,
            TotalCount = totalCount
        };
    }
    
    private DataTable CreateDataTable(List<AuditEvent> events)
    {
        var dt = new DataTable();
        dt.Columns.Add("EventId", typeof(Guid));
        dt.Columns.Add("Timestamp", typeof(DateTime));
        dt.Columns.Add("Actor", typeof(string));
        dt.Columns.Add("Action", typeof(string));
        dt.Columns.Add("EntityType", typeof(string));
        dt.Columns.Add("EntityId", typeof(string));
        dt.Columns.Add("CorrelationId", typeof(string));
        dt.Columns.Add("IpAddress", typeof(string));
        dt.Columns.Add("UserAgent", typeof(string));
        dt.Columns.Add("EventData", typeof(string));
        
        foreach (var evt in events)
        {
            dt.Rows.Add(
                evt.EventId,
                evt.Timestamp,
                evt.Actor,
                evt.Action,
                evt.EntityType ?? (object)DBNull.Value,
                evt.EntityId ?? (object)DBNull.Value,
                evt.CorrelationId ?? (object)DBNull.Value,
                evt.IpAddress ?? (object)DBNull.Value,
                evt.UserAgent ?? (object)DBNull.Value,
                evt.EventData ?? (object)DBNull.Value
            );
        }
        
        return dt;
    }
}

// Background service to flush buffer periodically
public class AuditBufferFlushService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AuditBufferFlushService> _logger;
    
    public AuditBufferFlushService(IServiceProvider serviceProvider, ILogger<AuditBufferFlushService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();
                
                // Flush every 5 seconds
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                
                // Trigger flush via private method (requires refactoring or public flush method)
                _logger.LogDebug("Flushing audit event buffer");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error flushing audit event buffer");
            }
        }
    }
}
```

### Client Library for Services

```csharp
// IntelliFin.Shared.Audit/IAuditClient.cs
public interface IAuditClient
{
    Task LogEventAsync(string action, string? entityType = null, string? entityId = null, object? eventData = null);
}

// IntelliFin.Shared.Audit/AuditClient.cs
public class AuditClient : IAuditClient
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuditClient> _logger;
    
    public AuditClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor, ILogger<AuditClient> logger)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }
    
    public async Task LogEventAsync(string action, string? entityType = null, string? entityId = null, object? eventData = null)
    {
        try
        {
            var context = _httpContextAccessor.HttpContext;
            var actor = context?.User?.Identity?.Name ?? "System";
            var correlationId = Activity.Current?.Id ?? context?.TraceIdentifier;
            var ipAddress = context?.Connection?.RemoteIpAddress?.ToString();
            var userAgent = context?.Request?.Headers["User-Agent"].ToString();
            
            var auditEvent = new
            {
                Timestamp = DateTime.UtcNow,
                Actor = actor,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                CorrelationId = correlationId,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                EventData = eventData != null ? JsonSerializer.Serialize(eventData) : null
            };
            
            var response = await _httpClient.PostAsJsonAsync("/api/admin/audit/events", auditEvent);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            // Log but don't throw - audit failures shouldn't break business operations
            _logger.LogError(ex, "Failed to log audit event: {Action}", action);
        }
    }
}

// Service registration
services.AddHttpClient<IAuditClient, AuditClient>(client =>
{
    client.BaseAddress = new Uri(configuration["AdminService:Url"]);
});
```

### Migration Script

```csharp
// Migrate existing audit events from FinancialService
public class AuditMigrationService
{
    private readonly FinancialDbContext _financialDb;
    private readonly AdminDbContext _adminDb;
    private readonly ILogger<AuditMigrationService> _logger;
    
    public async Task<MigrationResult> MigrateAuditEventsAsync()
    {
        var result = new MigrationResult();
        
        // Extract from FinancialService
        var sourceEvents = await _financialDb.AuditLogs
            .OrderBy(a => a.Timestamp)
            .ToListAsync();
        
        _logger.LogInformation("Migrating {Count} audit events from FinancialService", sourceEvents.Count);
        
        var batchSize = 1000;
        for (int i = 0; i < sourceEvents.Count; i += batchSize)
        {
            var batch = sourceEvents.Skip(i).Take(batchSize).ToList();
            
            var targetEvents = batch.Select(s => new AuditEvent
            {
                EventId = Guid.NewGuid(),
                Timestamp = s.Timestamp,
                Actor = s.UserId,  // Map from old schema
                Action = s.Action,
                EntityType = s.EntityType,
                EntityId = s.EntityId,
                CorrelationId = s.CorrelationId,
                EventData = s.Details,
                MigrationSource = "FinancialService"
            }).ToList();
            
            await _adminDb.AuditEvents.AddRangeAsync(targetEvents);
            await _adminDb.SaveChangesAsync();
            
            result.MigratedCount += targetEvents.Count;
            _logger.LogInformation("Migrated batch {BatchNum}: {Count} events", i / batchSize + 1, targetEvents.Count);
        }
        
        result.IsSuccess = result.MigratedCount == sourceEvents.Count;
        return result;
    }
}
```

---

## Integration Verification

### IV1: Backward Compatible Audit Queries
**Verification Steps**:
1. Query existing audit events from FinancialService API
2. Verify API redirects to Admin Service
3. Confirm historical audit events accessible via unified API

**Success Criteria**: Existing audit queries work via CR7 compatibility layer.

### IV2: Performance Under Load
**Verification Steps**:
1. Load test with 10,000 audit events/second
2. Measure ingestion latency (target <100ms p95)
3. Verify no data loss during burst traffic

**Success Criteria**: NFR4 performance target met.

### IV3: Audit Data Integrity Post-Migration
**Verification Steps**:
1. Compare record counts: FinancialService vs Admin Service
2. Sample 10% random validation of migrated events
3. Verify timestamps, actors, and event data integrity

**Success Criteria**: 100% data integrity, zero data loss.

---

## Testing Strategy

### Unit Tests
1. **AuditService.LogEventAsync**: Verify event added to buffer
2. **Batch Insert**: Test sp_InsertAuditEventsBatch with 1000 events
3. **Query Filters**: Test each filter combination

### Integration Tests
1. **End-to-End Audit Flow**: Service → Admin Service → Database → Query API
2. **RabbitMQ Async**: Publish audit event → Admin Service consumes → DB persisted
3. **Migration**: Run migration script → Validate data integrity

### Performance Tests
- **Load Test**: 10,000 events/sec sustained for 5 minutes
- **Query Performance**: 30-day range query <1 second
- **Batch Insert**: 1000 events <50ms

### Security Tests
1. **Authorization**: Verify only authorized users can query audit trail
2. **SQL Injection**: Test query filters with malicious input
3. **Data Exposure**: Ensure sensitive data in EventData is properly handled

---

## Risks and Mitigation

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| Migration data loss or corruption | High | Low | Extensive validation, backup before migration, keep FinancialService audit read-only for 90 days |
| Audit ingestion performance bottleneck | High | Medium | Batch processing, RabbitMQ async option, database indexing, connection pooling |
| Breaking existing audit queries during transition | Medium | Medium | CR7 compatibility layer, gradual cutover, dual audit logging during transition |
| Buffer overflow under extreme load | Medium | Low | Buffer size limits with overflow alerts, RabbitMQ fallback for bursts |

---

## Definition of Done (DoD)

- [ ] Admin Service database schema deployed with AuditEvents table
- [ ] Audit ingestion API implemented (single + batch endpoints)
- [ ] All 7 microservices refactored to use Admin Service audit client
- [ ] RabbitMQ async audit option configured and tested
- [ ] Migration script successfully migrates all FinancialService audit events
- [ ] Audit query API with filtering and pagination operational
- [ ] Performance testing confirms NFR4 compliance (10,000 events/sec)
- [ ] All integration verification criteria passed
- [ ] Documentation updated in `docs/domains/system-administration/audit-centralization.md`
- [ ] Code review completed
- [ ] Security review completed (data exposure, authorization)

---

## Related Documentation

### PRD References
- **Full PRD**: `../system-administration-control-plane-prd.md` (Lines 944-967)
- **Requirements**: FR12, NFR4

### Architecture References
- **Full Architecture**: `../system-administration-control-plane-architecture.md` (Section 4.1.2, Lines 398-503)
- **Database Schema**: Section 4.1.2

---

## Notes for Development Team

### Pre-Implementation Checklist
- [ ] Admin Service database deployed and accessible
- [ ] RabbitMQ audit exchange created if using async option
- [ ] FinancialService audit data backed up before migration
- [ ] Performance testing environment configured for 10K events/sec load test
- [ ] All service teams notified of IAuditClient library upgrade

### Post-Implementation Handoff
- Compliance team trained on new audit query API
- FinancialService audit marked read-only (retain for 90 days)
- Monitoring alerts configured for audit ingestion failures
- Dashboard created showing audit event ingestion rate and buffer size

---

**Story Created**: 2025-10-11  
**Last Updated**: 2025-10-11  
**Next Story**: Story 1.15 - Tamper-Evident Audit Chain Implementation
