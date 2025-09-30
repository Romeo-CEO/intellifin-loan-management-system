using IntelliFin.Communications.Models;

namespace IntelliFin.Communications.Services;

public interface IInAppNotificationService
{
    Task<InAppNotificationResponse> SendNotificationAsync(CreateInAppNotificationRequest request, CancellationToken cancellationToken = default);
    Task<BulkInAppNotificationResponse> SendBulkNotificationAsync(BulkInAppNotificationRequest request, CancellationToken cancellationToken = default);
    Task<GetNotificationsResponse> GetNotificationsAsync(string userId, GetNotificationsRequest request, CancellationToken cancellationToken = default);
    Task<InAppNotification?> GetNotificationAsync(string notificationId, string userId, CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(string userId, CancellationToken cancellationToken = default);
    Task MarkAsReadAsync(string notificationId, string userId, CancellationToken cancellationToken = default);
    Task MarkAsClickedAsync(string notificationId, string userId, string? actionId = null, CancellationToken cancellationToken = default);
    Task DismissNotificationAsync(string notificationId, string userId, CancellationToken cancellationToken = default);
    Task MarkAllAsReadAsync(string userId, CancellationToken cancellationToken = default);
    Task DeleteNotificationAsync(string notificationId, string userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<InAppNotification>> GetPendingNotificationsAsync(string userId, CancellationToken cancellationToken = default);
    
    // Preferences
    Task<NotificationPreferences> GetPreferencesAsync(string userId, CancellationToken cancellationToken = default);
    Task UpdatePreferencesAsync(string userId, UpdateNotificationPreferencesRequest request, CancellationToken cancellationToken = default);
    Task<bool> ShouldSendNotificationAsync(string userId, InAppNotification notification, CancellationToken cancellationToken = default);
    
    // Analytics
    Task<NotificationEngagementMetrics> GetEngagementMetricsAsync(string userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    
    // Cleanup
    Task CleanupExpiredNotificationsAsync(CancellationToken cancellationToken = default);
    Task ArchiveOldNotificationsAsync(int daysToKeep = 90, CancellationToken cancellationToken = default);
}

public interface INotificationConnectionManager
{
    Task AddConnectionAsync(string userId, string connectionId);
    Task RemoveConnectionAsync(string userId, string connectionId);
    Task<IEnumerable<string>> GetConnectionsAsync(string userId);
    Task<bool> IsUserOnlineAsync(string userId);
    Task<int> GetOnlineUsersCountAsync();
    Task<IEnumerable<string>> GetOnlineUsersAsync();
    Task RemoveAllConnectionsAsync(string userId);
}

public interface INotificationDeliveryService
{
    Task DeliverNotificationAsync(InAppNotification notification, CancellationToken cancellationToken = default);
    Task DeliverBulkNotificationsAsync(IEnumerable<InAppNotification> notifications, CancellationToken cancellationToken = default);
    Task DeliverToUserAsync(string userId, InAppNotification notification, CancellationToken cancellationToken = default);
    Task DeliverToGroupAsync(string groupName, InAppNotification notification, CancellationToken cancellationToken = default);
    Task DeliverToAllOnlineUsersAsync(InAppNotification notification, CancellationToken cancellationToken = default);
}

public interface INotificationTemplateService
{
    Task<InAppNotification> RenderNotificationAsync(string templateId, Dictionary<string, object> parameters, string userId, CancellationToken cancellationToken = default);
    Task<string> RenderMessageAsync(string template, Dictionary<string, object> parameters, CancellationToken cancellationToken = default);
    Task<InAppNotificationTemplate> CreateTemplateAsync(CreateNotificationTemplateRequest request, CancellationToken cancellationToken = default);
    Task<InAppNotificationTemplate> UpdateTemplateAsync(string templateId, CreateNotificationTemplateRequest request, CancellationToken cancellationToken = default);
    Task<InAppNotificationTemplate?> GetTemplateAsync(string templateId, CancellationToken cancellationToken = default);
    Task<IEnumerable<InAppNotificationTemplate>> GetTemplatesAsync(InAppNotificationCategory? category = null, CancellationToken cancellationToken = default);
    Task DeleteTemplateAsync(string templateId, CancellationToken cancellationToken = default);
    Task<List<string>> ValidateTemplateAsync(string templateId, CancellationToken cancellationToken = default);
}

public interface INotificationSchedulerService
{
    Task ScheduleNotificationAsync(CreateInAppNotificationRequest request, DateTime scheduledAt, CancellationToken cancellationToken = default);
    Task ScheduleBulkNotificationAsync(BulkInAppNotificationRequest request, DateTime scheduledAt, CancellationToken cancellationToken = default);
    Task CancelScheduledNotificationAsync(string notificationId, CancellationToken cancellationToken = default);
    Task<IEnumerable<InAppNotification>> GetScheduledNotificationsAsync(DateTime? upTo = null, CancellationToken cancellationToken = default);
    Task ProcessScheduledNotificationsAsync(CancellationToken cancellationToken = default);
    Task<bool> RescheduleNotificationAsync(string notificationId, DateTime newScheduledAt, CancellationToken cancellationToken = default);
}

// Additional models for templates
public class InAppNotificationTemplate
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TitleTemplate { get; set; } = string.Empty;
    public string MessageTemplate { get; set; } = string.Empty;
    public string? ActionUrlTemplate { get; set; }
    public List<string> Parameters { get; set; } = new();
    public InAppNotificationCategory Category { get; set; }
    public InAppNotificationType Type { get; set; }
    public InAppNotificationPriority Priority { get; set; } = InAppNotificationPriority.Normal;
    public string? Icon { get; set; }
    public List<InAppNotificationActionTemplate> ActionTemplates { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastModified { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class InAppNotificationActionTemplate
{
    public string Id { get; set; } = string.Empty;
    public string LabelTemplate { get; set; } = string.Empty;
    public string ActionUrlTemplate { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public string? Icon { get; set; }
    public Dictionary<string, object> DefaultParameters { get; set; } = new();
}

public class CreateNotificationTemplateRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TitleTemplate { get; set; } = string.Empty;
    public string MessageTemplate { get; set; } = string.Empty;
    public string? ActionUrlTemplate { get; set; }
    public InAppNotificationCategory Category { get; set; }
    public InAppNotificationType Type { get; set; } = InAppNotificationType.Info;
    public InAppNotificationPriority Priority { get; set; } = InAppNotificationPriority.Normal;
    public string? Icon { get; set; }
    public List<InAppNotificationActionTemplate> ActionTemplates { get; set; } = new();
    public Dictionary<string, string> Metadata { get; set; } = new();
}