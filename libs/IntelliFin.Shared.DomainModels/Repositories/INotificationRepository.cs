using IntelliFin.Shared.DomainModels.Entities;

namespace IntelliFin.Shared.DomainModels.Repositories;

public interface INotificationRepository
{
    // Create operations
    Task<NotificationLog> CreateAsync(NotificationLog log, CancellationToken cancellationToken = default);
    Task<List<NotificationLog>> CreateBulkAsync(List<NotificationLog> logs, CancellationToken cancellationToken = default);

    // Read operations
    Task<NotificationLog?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<NotificationLog?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default);
    Task<List<NotificationLog>> GetByRecipientAsync(string recipientId, string? channel = null,
        DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);
    Task<List<NotificationLog>> GetPagedAsync(NotificationLogQuery query, CancellationToken cancellationToken = default);

    // Update operations
    Task UpdateStatusAsync(long id, NotificationStatus status, string? gatewayResponse = null,
        string? failureReason = null, CancellationToken cancellationToken = default);
    Task IncrementRetryCountAsync(long id, CancellationToken cancellationToken = default);

    // Event processing
    Task<bool> IsEventProcessedAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task MarkEventProcessedAsync(Guid eventId, string eventType, bool success,
        string? error = null, CancellationToken cancellationToken = default);

    // Analytics
    Task<NotificationStats> GetStatsAsync(DateTime fromDate, DateTime toDate,
        string? branchId = null, CancellationToken cancellationToken = default);
}

public class NotificationLogQuery
{
    public string? RecipientId { get; set; }
    public string? Channel { get; set; }
    public string? EventType { get; set; }
    public NotificationStatus? Status { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? BranchId { get; set; }
}

public class NotificationStats
{
    public int TotalSent { get; set; }
    public int TotalDelivered { get; set; }
    public int TotalFailed { get; set; }
    public decimal TotalCost { get; set; }
    public double DeliveryRate => TotalSent > 0 ? (double)TotalDelivered / TotalSent * 100 : 0;
    public Dictionary<string, int> ChannelUsage { get; set; } = new();
    public Dictionary<string, int> StatusDistribution { get; set; } = new();
}
