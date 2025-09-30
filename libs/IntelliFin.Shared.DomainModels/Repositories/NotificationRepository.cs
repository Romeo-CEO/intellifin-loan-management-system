using IntelliFin.Shared.DomainModels.Data;
using IntelliFin.Shared.DomainModels.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IntelliFin.Shared.DomainModels.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly LmsDbContext _context;
    private readonly ILogger<NotificationRepository> _logger;

    public NotificationRepository(LmsDbContext context, ILogger<NotificationRepository>? logger = null)
    {
        _context = context;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<NotificationRepository>.Instance;
    }

    public async Task<NotificationLog> CreateAsync(NotificationLog log, CancellationToken cancellationToken = default)
    {
        _context.NotificationLogs.Add(log);
        await _context.SaveChangesAsync(cancellationToken);
        return log;
    }

    public async Task<List<NotificationLog>> CreateBulkAsync(List<NotificationLog> logs, CancellationToken cancellationToken = default)
    {
        await _context.NotificationLogs.AddRangeAsync(logs, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return logs;
    }

    public async Task<NotificationLog?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.NotificationLogs
            .Include(n => n.Template)
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
    }

    public async Task<NotificationLog?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default)
    {
        return await _context.NotificationLogs
            .Include(n => n.Template)
            .FirstOrDefaultAsync(n => n.ExternalId == externalId, cancellationToken);
    }

    public async Task<List<NotificationLog>> GetByRecipientAsync(string recipientId, string? channel = null,
        DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
    {
        var query = _context.NotificationLogs
            .Where(n => n.RecipientId == recipientId);

        if (!string.IsNullOrEmpty(channel))
            query = query.Where(n => n.Channel == channel);

        if (fromDate.HasValue)
            query = query.Where(n => n.CreatedAt >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(n => n.CreatedAt <= toDate.Value);

        return await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(100) // Limit to prevent large result sets
            .ToListAsync(cancellationToken);
    }

    public async Task<List<NotificationLog>> GetPagedAsync(NotificationLogQuery query, CancellationToken cancellationToken = default)
    {
        var dbQuery = _context.NotificationLogs.AsQueryable();

        // Filter by recipient
        if (!string.IsNullOrEmpty(query.RecipientId))
            dbQuery = dbQuery.Where(n => n.RecipientId == query.RecipientId);

        // Filter by channel
        if (!string.IsNullOrEmpty(query.Channel))
            dbQuery = dbQuery.Where(n => n.Channel == query.Channel);

        // Filter by status
        if (query.Status.HasValue)
            dbQuery = dbQuery.Where(n => n.Status == query.Status.Value);

        // Filter by date range
        if (query.FromDate.HasValue)
            dbQuery = dbQuery.Where(n => n.CreatedAt >= query.FromDate.Value);

        if (query.ToDate.HasValue)
            dbQuery = dbQuery.Where(n => n.CreatedAt <= query.ToDate.Value);

        // Filter by branch
        if (!string.IsNullOrEmpty(query.BranchId))
            dbQuery = dbQuery.Where(n => n.BranchId.ToString() == query.BranchId);

        return await dbQuery
            .OrderByDescending(n => n.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateStatusAsync(long id, NotificationStatus status, string? gatewayResponse = null,
        string? failureReason = null, CancellationToken cancellationToken = default)
    {
        var notification = await _context.NotificationLogs.FindAsync(new object[] { id }, cancellationToken);
        if (notification != null)
        {
            notification.Status = status;
            notification.GatewayResponse = gatewayResponse;
            notification.FailureReason = failureReason;

            if (status == NotificationStatus.Sent)
                notification.SentAt = DateTimeOffset.UtcNow;
            else if (status == NotificationStatus.Delivered)
                notification.DeliveredAt = DateTimeOffset.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task IncrementRetryCountAsync(long id, CancellationToken cancellationToken = default)
    {
        var notification = await _context.NotificationLogs.FindAsync(new object[] { id }, cancellationToken);
        if (notification != null)
        {
            notification.RetryCount++;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> IsEventProcessedAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        return await _context.EventProcessingStatus
            .AnyAsync(e => e.EventId == eventId, cancellationToken);
    }

    public async Task MarkEventProcessedAsync(Guid eventId, string eventType, bool success,
        string? error = null, CancellationToken cancellationToken = default)
    {
        var processingStatus = new EventProcessingStatus
        {
            EventId = eventId,
            EventType = eventType,
            ProcessingResult = success ? "Success" : "Failed",
            ErrorDetails = error
        };

        _context.EventProcessingStatus.Add(processingStatus);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<NotificationStats> GetStatsAsync(DateTime fromDate, DateTime toDate,
        string? branchId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.NotificationLogs
            .Where(n => n.CreatedAt >= fromDate && n.CreatedAt <= toDate);

        if (!string.IsNullOrEmpty(branchId))
            query = query.Where(n => n.BranchId.ToString() == branchId);

        var notifications = await query.ToListAsync(cancellationToken);

        var stats = new NotificationStats
        {
            TotalSent = notifications.Count(n => n.Status == NotificationStatus.Sent || n.Status == NotificationStatus.Delivered),
            TotalDelivered = notifications.Count(n => n.Status == NotificationStatus.Delivered),
            TotalFailed = notifications.Count(n => n.Status == NotificationStatus.Failed),
            TotalCost = notifications.Sum(n => n.Cost ?? 0),
            ChannelUsage = notifications.GroupBy(n => n.Channel).ToDictionary(g => g.Key, g => g.Count()),
            StatusDistribution = notifications.GroupBy(n => n.Status.ToString()).ToDictionary(g => g.Key, g => g.Count())
        };

        return stats;
    }
}
