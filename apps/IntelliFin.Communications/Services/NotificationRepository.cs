using IntelliFin.Shared.DomainModels.Data;
using IntelliFin.Shared.DomainModels.Entities;
using IntelliFin.Shared.DomainModels.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IntelliFin.Communications.Services;

/// <summary>
/// Entity Framework based implementation of <see cref="INotificationRepository"/> that
/// encapsulates the audit and idempotency data access requirements for the communications service.
/// </summary>
public class NotificationRepository : INotificationRepository
{
    private readonly LmsDbContext _dbContext;
    private readonly ILogger<NotificationRepository> _logger;

    public NotificationRepository(LmsDbContext dbContext, ILogger<NotificationRepository> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<NotificationLog> CreateAsync(NotificationLog log, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(log);

        await _dbContext.NotificationLogs.AddAsync(log, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Created notification log {NotificationLogId} for event {EventId}", log.Id, log.EventId);
        return log;
    }

    public async Task<List<NotificationLog>> CreateBulkAsync(List<NotificationLog> logs, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logs);

        await _dbContext.NotificationLogs.AddRangeAsync(logs, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Created {Count} notification log entries", logs.Count);
        return logs;
    }

    public Task<NotificationLog?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return _dbContext.NotificationLogs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<NotificationLog?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);

        return _dbContext.NotificationLogs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ExternalId == externalId, cancellationToken);
    }

    public async Task<List<NotificationLog>> GetByRecipientAsync(
        string recipientId,
        string? channel = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientId);

        var query = _dbContext.NotificationLogs.AsNoTracking().Where(x => x.RecipientId == recipientId);

        if (!string.IsNullOrWhiteSpace(channel))
        {
            query = query.Where(x => x.Channel == channel);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(x => x.CreatedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(x => x.CreatedAt <= toDate.Value);
        }

        return await query.OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task<List<NotificationLog>> GetPagedAsync(NotificationLogQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var baseQuery = _dbContext.NotificationLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.RecipientId))
        {
            baseQuery = baseQuery.Where(x => x.RecipientId == query.RecipientId);
        }

        if (!string.IsNullOrWhiteSpace(query.Channel))
        {
            baseQuery = baseQuery.Where(x => x.Channel == query.Channel);
        }

        if (!string.IsNullOrWhiteSpace(query.EventType))
        {
            baseQuery = baseQuery
                .Include(x => x.Template)
                .Where(x => x.Template != null && x.Template.Category == query.EventType);
        }

        if (query.Status.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.Status == query.Status.Value);
        }

        if (query.FromDate.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.CreatedAt >= query.FromDate.Value);
        }

        if (query.ToDate.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.CreatedAt <= query.ToDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.BranchId) && int.TryParse(query.BranchId, out var branchId))
        {
            baseQuery = baseQuery.Where(x => x.BranchId == branchId);
        }

        var skip = (query.Page - 1) * query.PageSize;
        return await baseQuery
            .OrderByDescending(x => x.CreatedAt)
            .Skip(skip)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateStatusAsync(
        long id,
        NotificationStatus status,
        string? gatewayResponse = null,
        string? failureReason = null,
        CancellationToken cancellationToken = default)
    {
        var log = await _dbContext.NotificationLogs.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (log == null)
        {
            _logger.LogWarning("Attempted to update notification log {NotificationLogId} but it was not found", id);
            return;
        }

        log.Status = status;
        log.GatewayResponse = gatewayResponse;
        log.FailureReason = failureReason;

        var now = DateTimeOffset.UtcNow;
        if (status is NotificationStatus.Sent or NotificationStatus.Delivered)
        {
            log.SentAt ??= now;
        }

        if (status == NotificationStatus.Delivered)
        {
            log.DeliveredAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task IncrementRetryCountAsync(long id, CancellationToken cancellationToken = default)
    {
        var log = await _dbContext.NotificationLogs.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (log == null)
        {
            _logger.LogWarning("Attempted to increment retry count for notification log {NotificationLogId} but it was not found", id);
            return;
        }

        log.RetryCount += 1;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> IsEventProcessedAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var status = await _dbContext.EventProcessingStatus.AsNoTracking()
            .FirstOrDefaultAsync(x => x.EventId == eventId, cancellationToken);

        return status is { ProcessingResult: "Success" };
    }

    public async Task MarkEventProcessedAsync(
        Guid eventId,
        string eventType,
        bool success,
        string? error = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);

        var status = await _dbContext.EventProcessingStatus
            .FirstOrDefaultAsync(x => x.EventId == eventId, cancellationToken);

        if (status == null)
        {
            status = new EventProcessingStatus
            {
                EventId = eventId,
                EventType = eventType,
                ProcessedAt = DateTimeOffset.UtcNow,
                ProcessingResult = success ? "Success" : "Failed",
                ErrorDetails = error
            };

            await _dbContext.EventProcessingStatus.AddAsync(status, cancellationToken);
        }
        else
        {
            status.ProcessedAt = DateTimeOffset.UtcNow;
            status.ProcessingResult = success ? "Success" : "Failed";
            status.ErrorDetails = error;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<NotificationStats> GetStatsAsync(
        DateTime fromDate,
        DateTime toDate,
        string? branchId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.NotificationLogs.AsNoTracking()
            .Where(x => x.CreatedAt >= fromDate && x.CreatedAt <= toDate);

        if (!string.IsNullOrWhiteSpace(branchId) && int.TryParse(branchId, out var parsedBranchId))
        {
            query = query.Where(x => x.BranchId == parsedBranchId);
        }

        var stats = new NotificationStats();
        var groupedByStatus = await query
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        foreach (var item in groupedByStatus)
        {
            stats.StatusDistribution[item.Status.ToString()] = item.Count;

            switch (item.Status)
            {
                case NotificationStatus.Sent:
                    stats.TotalSent += item.Count;
                    break;
                case NotificationStatus.Delivered:
                    stats.TotalDelivered += item.Count;
                    break;
                case NotificationStatus.Failed:
                    stats.TotalFailed += item.Count;
                    break;
            }
        }

        stats.TotalCost = await query.SumAsync(x => x.Cost ?? 0, cancellationToken);

        var channelUsage = await query
            .GroupBy(x => x.Channel)
            .Select(g => new { Channel = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        foreach (var item in channelUsage)
        {
            stats.ChannelUsage[item.Channel] = item.Count;
        }

        return stats;
    }
}
