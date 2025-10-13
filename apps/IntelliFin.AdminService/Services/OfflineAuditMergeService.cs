using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using IntelliFin.AdminService.Contracts.Requests;
using IntelliFin.AdminService.Data;
using IntelliFin.AdminService.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IntelliFin.AdminService.Services;

public sealed class OfflineAuditMergeService : IOfflineAuditMergeService
{
    private readonly AdminDbContext _dbContext;
    private readonly IAuditHashService _hashService;
    private readonly ILogger<OfflineAuditMergeService> _logger;

    public OfflineAuditMergeService(
        AdminDbContext dbContext,
        IAuditHashService hashService,
        ILogger<OfflineAuditMergeService> logger)
    {
        _dbContext = dbContext;
        _hashService = hashService;
        _logger = logger;
    }

    public async Task<OfflineMergeResult> MergeAsync(OfflineMergeRequest request, string userId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var mergeId = Guid.NewGuid();
        var stopwatch = Stopwatch.StartNew();
        var normalizedUser = string.IsNullOrWhiteSpace(userId) ? "unknown" : userId.Trim();
        var deviceId = Normalize(request.DeviceId) ?? "unknown-device";
        var sessionId = Normalize(request.OfflineSessionId) ?? Guid.NewGuid().ToString();

        var candidates = NormalizeEvents(request.Events);

        var result = new OfflineMergeResult
        {
            MergeId = mergeId,
            EventsReceived = candidates.Count
        };

        if (candidates.Count == 0)
        {
            stopwatch.Stop();
            result.Status = "SUCCESS";
            result.MergeDurationMs = (int)stopwatch.ElapsedMilliseconds;
            await LogHistoryAsync(deviceId, sessionId, normalizedUser, result, cancellationToken);
            return result;
        }

        try
        {
            var (existingConflicts, existingKeys) = await LoadExistingEventsAsync(candidates, cancellationToken);
            var filtered = FilterDuplicates(candidates, existingConflicts, existingKeys, result);

            if (filtered.Count == 0)
            {
                stopwatch.Stop();
                result.Status = "SUCCESS";
                result.MergeDurationMs = (int)stopwatch.ElapsedMilliseconds;
                await LogHistoryAsync(deviceId, sessionId, normalizedUser, result, cancellationToken);
                return result;
            }

            var auditEvents = CreateAuditEvents(filtered, deviceId, sessionId, mergeId);

            await using (var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken))
            {
                try
                {
                    await _dbContext.AuditEvents.AddRangeAsync(auditEvents, cancellationToken);
                    await _dbContext.SaveChangesAsync(cancellationToken);

                    result.EventsMerged = auditEvents.Count;

                    var rehashed = await RehashChainAsync(auditEvents, cancellationToken);
                    result.EventsReHashed = rehashed;

                    await _dbContext.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            }

            result.Status = DetermineStatus(result);
            stopwatch.Stop();
            result.MergeDurationMs = (int)stopwatch.ElapsedMilliseconds;

            await LogHistoryAsync(deviceId, sessionId, normalizedUser, result, cancellationToken);
            _logger.LogInformation(
                "Offline audit merge completed. MergeId: {MergeId}, EventsMerged: {Merged}, Duplicates: {Duplicates}, Conflicts: {Conflicts}, Rehashed: {Rehashed}",
                mergeId,
                result.EventsMerged,
                result.DuplicatesSkipped,
                result.ConflictsDetected,
                result.EventsReHashed);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.Status = "FAILED";
            result.ErrorDetails = ex.Message;
            result.MergeDurationMs = (int)stopwatch.ElapsedMilliseconds;

            _logger.LogError(ex, "Offline audit merge failed for merge {MergeId}", mergeId);

            try
            {
                await LogHistoryAsync(deviceId, sessionId, normalizedUser, result, cancellationToken);
            }
            catch (Exception historyEx)
            {
                _logger.LogError(historyEx, "Failed to persist offline merge history for merge {MergeId}", mergeId);
            }

            throw;
        }
    }

    private async Task<(List<ConflictCandidate> Conflicts, HashSet<DuplicateKey> DuplicateKeys)> LoadExistingEventsAsync(
        IReadOnlyList<OfflineEventCandidate> candidates,
        CancellationToken cancellationToken)
    {
        if (candidates.Count == 0)
        {
            return (new List<ConflictCandidate>(), new HashSet<DuplicateKey>());
        }

        var earliest = candidates.Min(evt => evt.Timestamp).AddSeconds(-5);
        var latest = candidates.Max(evt => evt.Timestamp).AddSeconds(5);

        var existing = await _dbContext.AuditEvents
            .AsNoTracking()
            .Where(evt => evt.Timestamp >= earliest && evt.Timestamp <= latest)
            .Select(evt => new
            {
                evt.Timestamp,
                evt.Actor,
                evt.Action,
                evt.EntityId,
                evt.CorrelationId
            })
            .ToListAsync(cancellationToken);

        var conflicts = existing
            .Select(evt => new ConflictCandidate(
                Normalize(evt.Actor) ?? string.Empty,
                Normalize(evt.Action) ?? string.Empty,
                Normalize(evt.EntityId),
                evt.Timestamp))
            .ToList();

        var duplicateKeys = new HashSet<DuplicateKey>(existing.Select(evt => CreateKey(evt.CorrelationId, evt.Timestamp, evt.Actor, evt.Action, evt.EntityId)));

        return (conflicts, duplicateKeys);
    }

    private static List<OfflineEventCandidate> NormalizeEvents(IEnumerable<OfflineAuditEventRequest> events)
    {
        var list = new List<OfflineEventCandidate>();

        foreach (var evt in events ?? Array.Empty<OfflineAuditEventRequest>())
        {
            var actor = Normalize(evt.Actor) ?? string.Empty;
            var action = Normalize(evt.Action) ?? string.Empty;
            if (string.IsNullOrEmpty(actor) || string.IsNullOrEmpty(action))
            {
                continue;
            }

            var eventId = !string.IsNullOrWhiteSpace(evt.EventId) && Guid.TryParse(evt.EventId, out var parsed)
                ? parsed
                : Guid.NewGuid();

            var timestamp = evt.Timestamp == default ? DateTime.UtcNow : evt.Timestamp.ToUniversalTime();
            var correlation = Normalize(evt.CorrelationId);
            if (string.IsNullOrWhiteSpace(correlation))
            {
                correlation = eventId.ToString();
            }

            list.Add(new OfflineEventCandidate
            {
                EventId = eventId,
                Timestamp = timestamp,
                Actor = actor,
                Action = action,
                EntityType = Normalize(evt.EntityType),
                EntityId = Normalize(evt.EntityId),
                CorrelationId = correlation,
                EventData = evt.EventData.HasValue ? NormalizeJson(evt.EventData.Value) : null
            });
        }

        return list
            .OrderBy(evt => evt.Timestamp)
            .ThenBy(evt => evt.EventId)
            .ToList();
    }

    private static List<OfflineEventCandidate> FilterDuplicates(
        IReadOnlyList<OfflineEventCandidate> candidates,
        IReadOnlyList<ConflictCandidate> existingConflicts,
        HashSet<DuplicateKey> existingKeys,
        OfflineMergeResult result)
    {
        var accepted = new List<OfflineEventCandidate>(candidates.Count);
        var acceptedConflicts = new List<ConflictCandidate>(candidates.Count);
        var seenKeys = new HashSet<DuplicateKey>();

        foreach (var candidate in candidates)
        {
            var key = CreateKey(candidate.CorrelationId, candidate.Timestamp, candidate.Actor, candidate.Action, candidate.EntityId);

            if (existingKeys.Contains(key) || seenKeys.Contains(key))
            {
                result.DuplicatesSkipped++;
                continue;
            }

            seenKeys.Add(key);

            if (HasConflict(candidate, existingConflicts) || HasConflict(candidate, acceptedConflicts))
            {
                result.ConflictsDetected++;
            }

            accepted.Add(candidate);
            acceptedConflicts.Add(new ConflictCandidate(candidate.Actor, candidate.Action, candidate.EntityId, candidate.Timestamp));
        }

        return accepted;
    }

    private List<AuditEvent> CreateAuditEvents(
        IEnumerable<OfflineEventCandidate> candidates,
        string deviceId,
        string sessionId,
        Guid mergeId)
    {
        var now = DateTime.UtcNow;
        return candidates.Select(candidate => new AuditEvent
        {
            EventId = candidate.EventId,
            Timestamp = candidate.Timestamp,
            Actor = candidate.Actor,
            Action = candidate.Action,
            EntityType = candidate.EntityType,
            EntityId = candidate.EntityId,
            CorrelationId = candidate.CorrelationId,
            EventData = candidate.EventData,
            IsOfflineEvent = true,
            OfflineDeviceId = deviceId,
            OfflineSessionId = sessionId,
            OfflineMergeId = mergeId,
            IntegrityStatus = "PENDING_REHASH",
            CreatedAt = now
        }).ToList();
    }

    private async Task<int> RehashChainAsync(IReadOnlyList<AuditEvent> insertedEvents, CancellationToken cancellationToken)
    {
        if (insertedEvents.Count == 0)
        {
            return 0;
        }

        var earliestTimestamp = insertedEvents.Min(evt => evt.Timestamp);
        var minInsertedId = insertedEvents.Min(evt => evt.Id);

        var priorEvent = await _dbContext.AuditEvents
            .Where(evt => evt.Timestamp < earliestTimestamp || (evt.Timestamp == earliestTimestamp && evt.Id < minInsertedId))
            .OrderByDescending(evt => evt.Timestamp)
            .ThenByDescending(evt => evt.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var eventsToRehash = await _dbContext.AuditEvents
            .Where(evt => evt.Timestamp > earliestTimestamp || (evt.Timestamp == earliestTimestamp && evt.Id >= minInsertedId))
            .OrderBy(evt => evt.Timestamp)
            .ThenBy(evt => evt.Id)
            .ToListAsync(cancellationToken);

        var previousHash = priorEvent?.CurrentEventHash;
        var rehashed = 0;

        foreach (var evt in eventsToRehash)
        {
            if (!string.IsNullOrEmpty(evt.CurrentEventHash) && string.IsNullOrEmpty(evt.OriginalHash))
            {
                evt.OriginalHash = evt.CurrentEventHash;
            }

            evt.PreviousEventHash = previousHash;
            evt.CurrentEventHash = _hashService.CalculateHash(evt, previousHash);
            evt.IntegrityStatus = "REHASHED";
            evt.LastVerifiedAt = null;
            evt.IsGenesisEvent = previousHash is null;

            previousHash = evt.CurrentEventHash;
            rehashed++;
        }

        return rehashed;
    }

    private async Task LogHistoryAsync(
        string deviceId,
        string sessionId,
        string userId,
        OfflineMergeResult result,
        CancellationToken cancellationToken)
    {
        var history = new OfflineMergeHistory
        {
            MergeId = result.MergeId,
            UserId = userId,
            DeviceId = deviceId,
            OfflineSessionId = sessionId,
            EventsReceived = result.EventsReceived,
            EventsMerged = result.EventsMerged,
            DuplicatesSkipped = result.DuplicatesSkipped,
            ConflictsDetected = result.ConflictsDetected,
            EventsReHashed = result.EventsReHashed,
            MergeDurationMs = result.MergeDurationMs,
            Status = result.Status,
            ErrorDetails = result.ErrorDetails
        };

        await _dbContext.OfflineMergeHistory.AddAsync(history, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string DetermineStatus(OfflineMergeResult result)
    {
        if (result.EventsMerged == 0)
        {
            return "SUCCESS";
        }

        return result.DuplicatesSkipped > 0 || result.ConflictsDetected > 0
            ? "PARTIAL_SUCCESS"
            : "SUCCESS";
    }

    private static bool HasConflict(OfflineEventCandidate candidate, IEnumerable<ConflictCandidate> others)
    {
        foreach (var other in others)
        {
            if (!string.Equals(other.Actor, candidate.Actor, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.Equals(other.Action, candidate.Action, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.Equals(other.EntityId ?? string.Empty, candidate.EntityId ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (Math.Abs((other.Timestamp - candidate.Timestamp).TotalSeconds) <= 5)
            {
                return true;
            }
        }

        return false;
    }

    private static DuplicateKey CreateKey(string? correlationId, DateTime timestamp, string? actor, string? action, string? entityId)
        => new(
            NormalizeForComparison(correlationId),
            timestamp,
            NormalizeForComparison(actor),
            NormalizeForComparison(action),
            NormalizeForComparison(entityId));

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeForComparison(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();

    private static string? NormalizeJson(JsonElement element)
    {
        try
        {
            return JsonSerializer.Serialize(element);
        }
        catch (Exception)
        {
            return element.GetRawText();
        }
    }

    private sealed record OfflineEventCandidate
    {
        public required Guid EventId { get; init; }
        public required DateTime Timestamp { get; init; }
        public required string Actor { get; init; }
        public required string Action { get; init; }
        public string? EntityType { get; init; }
        public string? EntityId { get; init; }
        public required string CorrelationId { get; init; }
        public string? EventData { get; init; }
    }

    private readonly record struct ConflictCandidate(string Actor, string Action, string? EntityId, DateTime Timestamp);

    private readonly record struct DuplicateKey(string CorrelationId, DateTime Timestamp, string Actor, string Action, string EntityId);
}
