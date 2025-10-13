using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using IntelliFin.AdminService.Data;
using IntelliFin.AdminService.Models;
using IntelliFin.AdminService.Options;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntelliFin.AdminService.Services;

public sealed class AuditService : IAuditService
{
    private readonly AdminDbContext _dbContext;
    private readonly ILogger<AuditService> _logger;
    private readonly IAuditHashService _hashService;
    private AuditIngestionOptions _options;
    private readonly ConcurrentQueue<AuditEvent> _buffer = new();
    private readonly SemaphoreSlim _flushLock = new(1, 1);
    private DateTime _lastFlushUtc = DateTime.UtcNow;
    private bool _chainStateLoaded;
    private string? _lastPersistedHash;

    private const string IntegrityStatusValid = "VALID";
    private const string IntegrityStatusBroken = "BROKEN";
    private const string IntegrityStatusTampered = "TAMPERED";
    private const string IntegrityStatusUnverified = "UNVERIFIED";

    public AuditService(
        AdminDbContext dbContext,
        IAuditHashService hashService,
        IOptionsMonitor<AuditIngestionOptions> options,
        ILogger<AuditService> logger)
    {
        _dbContext = dbContext;
        _hashService = hashService;
        _logger = logger;
        _options = options.CurrentValue;
        options.OnChange(updated => _options = updated);
    }

    public async Task LogEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        var normalized = NormalizeEvent(auditEvent);

        if (_buffer.Count >= _options.MaxBufferSize)
        {
            _logger.LogError("Audit buffer has exceeded max size {Max} – rejecting event {EventId}", _options.MaxBufferSize, normalized.EventId);
            throw new InvalidOperationException("Audit ingestion buffer is full");
        }

        _buffer.Enqueue(normalized);

        if (_buffer.Count >= _options.BatchSize)
        {
            await FlushBufferAsync(cancellationToken);
        }
    }

    public async Task<int> LogEventsBatchAsync(IEnumerable<AuditEvent> auditEvents, CancellationToken cancellationToken)
    {
        var materialized = auditEvents.Select(NormalizeEvent).ToList();

        if (materialized.Count == 0)
        {
            return 0;
        }

        if (materialized.Count > _options.BatchSize)
        {
            throw new InvalidOperationException($"Batch size cannot exceed {_options.BatchSize} events");
        }

        foreach (var evt in materialized)
        {
            _buffer.Enqueue(evt);
        }

        await FlushBufferAsync(cancellationToken);
        return materialized.Count;
    }

    public async Task<AuditEventPage> GetAuditEventsAsync(AuditEventFilter filter, CancellationToken cancellationToken)
    {
        var safeFilter = NormalizeFilter(filter);

        var query = ApplyFilter(_dbContext.AuditEvents.AsNoTracking(), safeFilter);

        var totalCount = await query.CountAsync(cancellationToken);

        var events = await query
            .OrderByDescending(e => e.Timestamp)
            .Skip((safeFilter.Page - 1) * safeFilter.PageSize)
            .Take(safeFilter.PageSize)
            .ToListAsync(cancellationToken);

        return new AuditEventPage
        {
            Events = events,
            TotalCount = totalCount,
            Page = safeFilter.Page,
            PageSize = safeFilter.PageSize
        };
    }

    public async Task<IReadOnlyList<AuditEvent>> GetAllAuditEventsAsync(AuditEventFilter filter, CancellationToken cancellationToken)
    {
        var safeFilter = NormalizeFilter(filter);
        var query = ApplyFilter(_dbContext.AuditEvents.AsNoTracking(), safeFilter)
            .OrderByDescending(e => e.Timestamp);

        return await query.ToListAsync(cancellationToken);
    }

    public async Task FlushBufferAsync(CancellationToken cancellationToken)
    {
        if (!_buffer.Any())
        {
            return;
        }

        if (!await _flushLock.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            return;
        }

        var batch = new List<AuditEvent>(_options.BatchSize);

        try
        {
            
            foreach (var evt in _buffer)
            {
                batch.Add(evt);

                if (batch.Count >= _options.BatchSize)
                {
                    break;
                }
            }

            if (batch.Count == 0)
            {
                return;
            }

            await PersistBatchAsync(batch, cancellationToken);

            for (var i = 0; i < batch.Count; i++)
            {
                if (!_buffer.TryDequeue(out var dequeued))
                {
                    _logger.LogWarning("Expected to dequeue persisted audit event but buffer was empty");
                    break;
                }

                if (!ReferenceEquals(dequeued, batch[i]))
                {
                    _logger.LogWarning(
                        "Dequeued audit event {DequeuedEventId} did not match persisted batch event {BatchEventId}",
                        dequeued.EventId,
                        batch[i].EventId);
                }
            }

            _lastFlushUtc = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush audit event buffer; will retry batch of {Count} events", batch.Count);
            foreach (var evt in batch)
            {
                _logger.LogDebug("Buffered event {EventId} pending retry", evt.EventId);
            }
        }
        finally
        {
            _flushLock.Release();
        }
    }

    public AuditBufferMetrics GetBufferMetrics() => new()
    {
        BufferedEvents = _buffer.Count,
        BatchSize = _options.BatchSize,
        MaxBufferSize = _options.MaxBufferSize,
        LastFlushUtc = _lastFlushUtc
    };

    public async Task<ChainVerificationResult> VerifyChainIntegrityAsync(
        DateTime? startDate,
        DateTime? endDate,
        string initiatedBy,
        CancellationToken cancellationToken)
    {
        var verification = new AuditChainVerification
        {
            VerificationId = Guid.NewGuid(),
            StartTime = DateTime.UtcNow,
            InitiatedBy = string.IsNullOrWhiteSpace(initiatedBy) ? "System" : initiatedBy.Trim()
        };

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var query = _dbContext.AuditEvents.AsQueryable();

            if (startDate.HasValue)
            {
                query = query.Where(e => e.Timestamp >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(e => e.Timestamp <= endDate.Value);
            }

            var events = await query
                .OrderBy(e => e.Timestamp)
                .ThenBy(e => e.Id)
                .ToListAsync(cancellationToken);

            if (events.Count == 0)
            {
                verification.EventsVerified = 0;
                verification.ChainStatus = IntegrityStatusValid;
                verification.EndTime = DateTime.UtcNow;
                verification.VerificationDurationMs = (int)stopwatch.ElapsedMilliseconds;
                stopwatch.Stop();
                _dbContext.AuditChainVerifications.Add(verification);
                await _dbContext.SaveChangesAsync(cancellationToken);

                return new ChainVerificationResult
                {
                    Status = ChainStatus.Valid,
                    EventsVerified = 0,
                    DurationMs = verification.VerificationDurationMs
                };
            }

            var now = DateTime.UtcNow;
            string? previousHash = null;
            var treatAsGenesis = true;

            if (events.Count > 0)
            {
                var previousEvent = await GetPreviousEventAsync(events[0], cancellationToken);
                if (previousEvent is not null)
                {
                    previousHash = previousEvent.CurrentEventHash;
                    treatAsGenesis = false;
                }
            }

            ChainStatus status = ChainStatus.Valid;
            AuditEvent? failureEvent = null;
            string failureDescription = string.Empty;
            var processedCount = 0;

            for (var index = 0; index < events.Count; index++)
            {
                var evt = events[index];

                if (index == 0)
                {
                    if (treatAsGenesis)
                    {
                        if (!evt.IsGenesisEvent)
                        {
                            evt.IsGenesisEvent = true;
                        }

                        if (!string.IsNullOrEmpty(evt.PreviousEventHash))
                        {
                            status = ChainStatus.Broken;
                            failureDescription = "Genesis event has non-null previous hash";
                            failureEvent = evt;
                            processedCount = index + 1;
                            break;
                        }
                    }
                    else if (!string.Equals(evt.PreviousEventHash, previousHash, StringComparison.OrdinalIgnoreCase))
                    {
                        status = ChainStatus.Broken;
                        failureDescription = $"Previous hash mismatch. Expected {previousHash ?? "<null>"}, actual {evt.PreviousEventHash ?? "<null>"}";
                        failureEvent = evt;
                        processedCount = index + 1;
                        break;
                    }
                }
                else if (!string.Equals(evt.PreviousEventHash, previousHash, StringComparison.OrdinalIgnoreCase))
                {
                    status = ChainStatus.Broken;
                    failureDescription = $"Previous hash mismatch. Expected {previousHash ?? "<null>"}, actual {evt.PreviousEventHash ?? "<null>"}";
                    failureEvent = evt;
                    processedCount = index + 1;
                    break;
                }

                if (string.IsNullOrWhiteSpace(evt.CurrentEventHash))
                {
                    status = ChainStatus.Broken;
                    failureDescription = "Current hash missing";
                    failureEvent = evt;
                    processedCount = index + 1;
                    break;
                }

                if (!_hashService.VerifyHash(evt, previousHash))
                {
                    status = ChainStatus.Tampered;
                    failureDescription = "Hash verification failed - possible tampering";
                    failureEvent = evt;
                    processedCount = index + 1;
                    break;
                }

                evt.IntegrityStatus = IntegrityStatusValid;
                evt.LastVerifiedAt = now;
                previousHash = evt.CurrentEventHash;
                processedCount = index + 1;
                treatAsGenesis = false;
            }

            verification.EventsVerified = processedCount;

            if (status == ChainStatus.Valid)
            {
                verification.ChainStatus = IntegrityStatusValid;
            }
            else if (failureEvent is not null)
            {
                verification.ChainStatus = status == ChainStatus.Tampered ? IntegrityStatusTampered : IntegrityStatusBroken;
                verification.BrokenEventId = failureEvent.Id;
                verification.BrokenEventTimestamp = failureEvent.Timestamp;
                failureEvent.IntegrityStatus = status == ChainStatus.Tampered ? IntegrityStatusTampered : IntegrityStatusBroken;
                failureEvent.LastVerifiedAt = now;
                await LogChainIncidentAsync(failureEvent, failureDescription, status, cancellationToken);
            }

            verification.EndTime = DateTime.UtcNow;
            verification.VerificationDurationMs = (int)stopwatch.ElapsedMilliseconds;
            stopwatch.Stop();

            _dbContext.AuditChainVerifications.Add(verification);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new ChainVerificationResult
            {
                Status = status,
                EventsVerified = processedCount,
                BrokenEventId = verification.BrokenEventId,
                BrokenEventTimestamp = verification.BrokenEventTimestamp,
                DurationMs = verification.VerificationDurationMs
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            verification.ChainStatus = "ERROR";
            verification.EndTime = DateTime.UtcNow;
            verification.VerificationDurationMs = (int)stopwatch.ElapsedMilliseconds;
            _dbContext.AuditChainVerifications.Add(verification);
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogError(ex, "Failed to verify audit chain integrity");
            throw;
        }
    }

    public async Task<AuditIntegrityStatus> GetIntegrityStatusAsync(CancellationToken cancellationToken)
    {
        var lastVerification = await _dbContext.AuditChainVerifications
            .OrderByDescending(v => v.StartTime)
            .FirstOrDefaultAsync(cancellationToken);

        var totalEvents = await _dbContext.AuditEvents.CountAsync(cancellationToken);
        var verifiedEvents = await _dbContext.AuditEvents.CountAsync(e => e.IntegrityStatus == IntegrityStatusValid, cancellationToken);
        var brokenEvents = await _dbContext.AuditEvents.CountAsync(e => e.IntegrityStatus == IntegrityStatusBroken || e.IntegrityStatus == IntegrityStatusTampered, cancellationToken);

        var coverage = totalEvents == 0 ? 0d : Math.Round(verifiedEvents / (double)totalEvents * 100d, 2, MidpointRounding.AwayFromZero);

        return new AuditIntegrityStatus
        {
            LastVerification = lastVerification,
            TotalEvents = totalEvents,
            VerifiedEvents = verifiedEvents,
            BrokenEvents = brokenEvents,
            CoveragePercentage = coverage
        };
    }

    public async Task<VerificationHistoryPage> GetVerificationHistoryAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var safePage = page <= 0 ? 1 : page;
        var safeSize = Math.Clamp(pageSize, 1, 1_000);

        var query = _dbContext.AuditChainVerifications
            .OrderByDescending(v => v.StartTime);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((safePage - 1) * safeSize)
            .Take(safeSize)
            .ToListAsync(cancellationToken);

        return new VerificationHistoryPage
        {
            Items = items,
            TotalCount = total,
            Page = safePage,
            PageSize = safeSize
        };
    }

    private async Task LogChainIncidentAsync(AuditEvent failureEvent, string description, ChainStatus status, CancellationToken cancellationToken)
    {
        var incident = new SecurityIncident
        {
            IncidentType = "AUDIT_CHAIN_BREAK",
            Severity = status == ChainStatus.Tampered ? "CRITICAL" : "HIGH",
            Description = description,
            AffectedEntityType = nameof(AuditEvent),
            AffectedEntityId = failureEvent.Id.ToString(),
            ResolutionStatus = "OPEN",
            DetectedAt = DateTime.UtcNow
        };

        await _dbContext.SecurityIncidents.AddAsync(incident, cancellationToken);
        _logger.LogCritical(
            "Audit chain {Status} detected at event {EventId}. Description: {Description}",
            status,
            failureEvent.EventId,
            description);
    }

    private async Task PersistBatchAsync(List<AuditEvent> events, CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return;
        }

        await EnsureChainStateAsync(cancellationToken);

        var previousHash = _lastPersistedHash;

        foreach (var evt in events)
        {
            evt.PreviousEventHash = previousHash;
            evt.IsGenesisEvent = previousHash is null;
            evt.IntegrityStatus = IntegrityStatusUnverified;
            evt.LastVerifiedAt = null;
            evt.CurrentEventHash = _hashService.CalculateHash(evt, previousHash);
            previousHash = evt.CurrentEventHash;
        }

        await using var connection = (SqlConnection)_dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "sp_InsertAuditEventsBatch";
        command.CommandType = CommandType.StoredProcedure;

        var tvp = command.Parameters.AddWithValue("@Events", CreateDataTable(events));
        tvp.SqlDbType = SqlDbType.Structured;
        tvp.TypeName = "AuditEventTableType";

        var inserted = await command.ExecuteScalarAsync(cancellationToken);
        _lastPersistedHash = previousHash;
        _chainStateLoaded = true;
        _logger.LogDebug("Persisted {Count} audit events", inserted);
    }

    private static DataTable CreateDataTable(IEnumerable<AuditEvent> events)
    {
        var table = new DataTable();
        table.Columns.Add("EventId", typeof(Guid));
        table.Columns.Add("Timestamp", typeof(DateTime));
        table.Columns.Add("Actor", typeof(string));
        table.Columns.Add("Action", typeof(string));
        table.Columns.Add("EntityType", typeof(string));
        table.Columns.Add("EntityId", typeof(string));
        table.Columns.Add("CorrelationId", typeof(string));
        table.Columns.Add("IpAddress", typeof(string));
        table.Columns.Add("UserAgent", typeof(string));
        table.Columns.Add("EventData", typeof(string));
        table.Columns.Add("PreviousEventHash", typeof(string));
        table.Columns.Add("CurrentEventHash", typeof(string));
        table.Columns.Add("IntegrityStatus", typeof(string));
        table.Columns.Add("IsGenesisEvent", typeof(bool));
        table.Columns.Add("LastVerifiedAt", typeof(DateTime));

        foreach (var evt in events)
        {
            table.Rows.Add(
                evt.EventId,
                evt.Timestamp,
                evt.Actor,
                evt.Action,
                evt.EntityType ?? (object)DBNull.Value,
                evt.EntityId ?? (object)DBNull.Value,
                evt.CorrelationId ?? (object)DBNull.Value,
                evt.IpAddress ?? (object)DBNull.Value,
                evt.UserAgent ?? (object)DBNull.Value,
                evt.EventData ?? (object)DBNull.Value,
                evt.PreviousEventHash ?? (object)DBNull.Value,
                evt.CurrentEventHash ?? (object)DBNull.Value,
                evt.IntegrityStatus,
                evt.IsGenesisEvent,
                evt.LastVerifiedAt ?? (object)DBNull.Value);
        }

        return table;
    }

    private async Task EnsureChainStateAsync(CancellationToken cancellationToken)
    {
        if (_chainStateLoaded)
        {
            return;
        }

        var lastEvent = await _dbContext.AuditEvents
            .OrderByDescending(e => e.Id)
            .Select(e => new { e.CurrentEventHash })
            .FirstOrDefaultAsync(cancellationToken);

        _lastPersistedHash = lastEvent?.CurrentEventHash;
        _chainStateLoaded = true;
    }

    private async Task<AuditEvent?> GetPreviousEventAsync(AuditEvent firstEvent, CancellationToken cancellationToken)
        => await _dbContext.AuditEvents
            .Where(e => e.Timestamp < firstEvent.Timestamp
                        || (e.Timestamp == firstEvent.Timestamp && e.Id < firstEvent.Id))
            .OrderByDescending(e => e.Timestamp)
            .ThenByDescending(e => e.Id)
            .FirstOrDefaultAsync(cancellationToken);

    private static IQueryable<AuditEvent> ApplyFilter(IQueryable<AuditEvent> query, AuditEventFilter filter)
    {
        query = query.Where(e => e.Timestamp >= filter.StartDate && e.Timestamp <= filter.EndDate);

        if (!string.IsNullOrWhiteSpace(filter.Actor))
        {
            query = query.Where(e => e.Actor == filter.Actor);
        }

        if (!string.IsNullOrWhiteSpace(filter.Action))
        {
            query = query.Where(e => e.Action == filter.Action);
        }

        if (!string.IsNullOrWhiteSpace(filter.EntityType))
        {
            query = query.Where(e => e.EntityType == filter.EntityType);
        }

        if (!string.IsNullOrWhiteSpace(filter.EntityId))
        {
            query = query.Where(e => e.EntityId == filter.EntityId);
        }

        if (!string.IsNullOrWhiteSpace(filter.CorrelationId))
        {
            query = query.Where(e => e.CorrelationId == filter.CorrelationId);
        }

        return query;
    }

    private static AuditEventFilter NormalizeFilter(AuditEventFilter filter)
    {
        var start = filter.StartDate == default ? DateTime.UtcNow.AddDays(-30) : filter.StartDate;
        var end = filter.EndDate == default ? DateTime.UtcNow : filter.EndDate;

        return new AuditEventFilter
        {
            StartDate = start,
            EndDate = end < start ? start : end,
            Actor = Normalize(filter.Actor),
            Action = Normalize(filter.Action),
            EntityType = Normalize(filter.EntityType),
            EntityId = Normalize(filter.EntityId),
            CorrelationId = Normalize(filter.CorrelationId),
            Page = filter.Page <= 0 ? 1 : filter.Page,
            PageSize = Math.Clamp(filter.PageSize, 1, 1_000)
        };
    }

    private static AuditEvent NormalizeEvent(AuditEvent auditEvent)
    {
        auditEvent.EventId = auditEvent.EventId == Guid.Empty ? Guid.NewGuid() : auditEvent.EventId;
        auditEvent.Timestamp = auditEvent.Timestamp == default ? DateTime.UtcNow : auditEvent.Timestamp.ToUniversalTime();
        auditEvent.CreatedAt = auditEvent.CreatedAt == default ? DateTime.UtcNow : auditEvent.CreatedAt;
        auditEvent.Actor = (auditEvent.Actor ?? string.Empty).Trim();
        auditEvent.Action = (auditEvent.Action ?? string.Empty).Trim();
        auditEvent.EntityType = Normalize(auditEvent.EntityType);
        auditEvent.EntityId = Normalize(auditEvent.EntityId);
        auditEvent.CorrelationId = Normalize(auditEvent.CorrelationId);
        if (string.IsNullOrWhiteSpace(auditEvent.CorrelationId) && Activity.Current is { TraceId: { } traceId })
        {
            auditEvent.CorrelationId = traceId.ToString();
        }
        auditEvent.IpAddress = Normalize(auditEvent.IpAddress);
        auditEvent.UserAgent = Normalize(auditEvent.UserAgent);
        auditEvent.EventData = NormalizeJson(auditEvent.EventData);
        auditEvent.MigrationSource = Normalize(auditEvent.MigrationSource);
        auditEvent.IntegrityStatus = IntegrityStatusUnverified;
        auditEvent.LastVerifiedAt = null;
        auditEvent.PreviousEventHash = null;
        auditEvent.CurrentEventHash = null;
        auditEvent.IsGenesisEvent = false;
        auditEvent.IsOfflineEvent = false;
        auditEvent.OfflineDeviceId = null;
        auditEvent.OfflineSessionId = null;
        auditEvent.OfflineMergeId = null;
        auditEvent.OriginalHash = null;
        return auditEvent;
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            return JsonSerializer.Serialize(document.RootElement);
        }
        catch (JsonException)
        {
            return value;
        }
    }
}
