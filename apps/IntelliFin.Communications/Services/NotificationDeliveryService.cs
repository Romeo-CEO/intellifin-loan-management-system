using Microsoft.AspNetCore.SignalR;
using IntelliFin.Communications.Models;
using IntelliFin.Communications.Hubs;

namespace IntelliFin.Communications.Services;

public class NotificationDeliveryService : INotificationDeliveryService
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly INotificationConnectionManager _connectionManager;
    private readonly ILogger<NotificationDeliveryService> _logger;

    public NotificationDeliveryService(
        IHubContext<NotificationHub> hubContext,
        INotificationConnectionManager connectionManager,
        ILogger<NotificationDeliveryService> logger)
    {
        _hubContext = hubContext;
        _connectionManager = connectionManager;
        _logger = logger;
    }

    public async Task DeliverNotificationAsync(InAppNotification notification, CancellationToken cancellationToken = default)
    {
        await DeliverToUserAsync(notification.UserId, notification, cancellationToken);
    }

    public async Task DeliverBulkNotificationsAsync(IEnumerable<InAppNotification> notifications, CancellationToken cancellationToken = default)
    {
        var deliveryTasks = notifications.Select(n => DeliverToUserAsync(n.UserId, n, cancellationToken));
        await Task.WhenAll(deliveryTasks);
    }

    public async Task DeliverToUserAsync(string userId, InAppNotification notification, CancellationToken cancellationToken = default)
    {
        try
        {
            var connections = await _connectionManager.GetConnectionsAsync(userId);
            
            if (!connections.Any())
            {
                _logger.LogDebug("User {UserId} is not online, notification {NotificationId} will be delivered when they connect", 
                    userId, notification.Id);
                return;
            }

            var notificationEvent = new RealTimeNotificationEvent
            {
                EventId = Guid.NewGuid().ToString(),
                UserId = userId,
                Notification = notification,
                EventType = RealTimeEventType.NotificationCreated
            };

            // Send to all user connections
            await _hubContext.Clients.Group($"User_{userId}")
                .SendAsync("NotificationReceived", notificationEvent, cancellationToken);

            _logger.LogDebug("Delivered notification {NotificationId} to {ConnectionCount} connections for user {UserId}", 
                notification.Id, connections.Count(), userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error delivering notification {NotificationId} to user {UserId}", 
                notification.Id, userId);
            throw;
        }
    }

    public async Task DeliverToGroupAsync(string groupName, InAppNotification notification, CancellationToken cancellationToken = default)
    {
        try
        {
            var notificationEvent = new RealTimeNotificationEvent
            {
                EventId = Guid.NewGuid().ToString(),
                UserId = string.Empty, // Group notification
                Notification = notification,
                EventType = RealTimeEventType.NotificationCreated
            };

            await _hubContext.Clients.Group(groupName)
                .SendAsync("NotificationReceived", notificationEvent, cancellationToken);

            _logger.LogDebug("Delivered notification {NotificationId} to group {GroupName}", 
                notification.Id, groupName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error delivering notification {NotificationId} to group {GroupName}", 
                notification.Id, groupName);
            throw;
        }
    }

    public async Task DeliverToAllOnlineUsersAsync(InAppNotification notification, CancellationToken cancellationToken = default)
    {
        try
        {
            var notificationEvent = new RealTimeNotificationEvent
            {
                EventId = Guid.NewGuid().ToString(),
                UserId = string.Empty, // Broadcast notification
                Notification = notification,
                EventType = RealTimeEventType.NotificationCreated
            };

            await _hubContext.Clients.All
                .SendAsync("NotificationReceived", notificationEvent, cancellationToken);

            _logger.LogDebug("Delivered notification {NotificationId} to all online users", notification.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error delivering notification {NotificationId} to all online users", 
                notification.Id);
            throw;
        }
    }
}