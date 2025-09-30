using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using IntelliFin.Communications.Models;
using IntelliFin.Communications.Services;
using System.Security.Claims;

namespace IntelliFin.Communications.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    private readonly IInAppNotificationService _notificationService;
    private readonly ILogger<NotificationHub> _logger;
    private readonly INotificationConnectionManager _connectionManager;

    public NotificationHub(
        IInAppNotificationService notificationService,
        ILogger<NotificationHub> logger,
        INotificationConnectionManager connectionManager)
    {
        _notificationService = notificationService;
        _logger = logger;
        _connectionManager = connectionManager;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (!string.IsNullOrEmpty(userId))
        {
            await _connectionManager.AddConnectionAsync(userId, Context.ConnectionId);
            await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");
            
            _logger.LogInformation("User {UserId} connected with connection {ConnectionId}", userId, Context.ConnectionId);
            
            // Send connection established event
            var connectionEvent = new RealTimeNotificationEvent
            {
                EventId = Guid.NewGuid().ToString(),
                UserId = userId,
                EventType = RealTimeEventType.ConnectionEstablished,
                EventData = new Dictionary<string, object>
                {
                    ["connectionId"] = Context.ConnectionId,
                    ["timestamp"] = DateTime.UtcNow
                }
            };
            
            await Clients.Caller.SendAsync("ConnectionEstablished", connectionEvent);
            
            // Send any pending notifications
            await SendPendingNotifications(userId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (!string.IsNullOrEmpty(userId))
        {
            await _connectionManager.RemoveConnectionAsync(userId, Context.ConnectionId);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"User_{userId}");
            
            _logger.LogInformation("User {UserId} disconnected from connection {ConnectionId}", userId, Context.ConnectionId);
            
            if (exception != null)
            {
                _logger.LogError(exception, "User {UserId} disconnected with error", userId);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task MarkAsRead(string notificationId)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId)) return;

        try
        {
            await _notificationService.MarkAsReadAsync(notificationId, userId);
            
            var readEvent = new RealTimeNotificationEvent
            {
                EventId = Guid.NewGuid().ToString(),
                UserId = userId,
                EventType = RealTimeEventType.NotificationRead,
                EventData = new Dictionary<string, object>
                {
                    ["notificationId"] = notificationId,
                    ["readAt"] = DateTime.UtcNow
                }
            };
            
            await Clients.Caller.SendAsync("NotificationRead", readEvent);
            
            _logger.LogDebug("Notification {NotificationId} marked as read by user {UserId}", notificationId, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking notification {NotificationId} as read for user {UserId}", notificationId, userId);
            await Clients.Caller.SendAsync("Error", new { message = "Failed to mark notification as read", notificationId });
        }
    }

    public async Task MarkAsClicked(string notificationId, string? actionId = null)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId)) return;

        try
        {
            await _notificationService.MarkAsClickedAsync(notificationId, userId, actionId);
            
            var clickedEvent = new RealTimeNotificationEvent
            {
                EventId = Guid.NewGuid().ToString(),
                UserId = userId,
                EventType = RealTimeEventType.NotificationClicked,
                EventData = new Dictionary<string, object>
                {
                    ["notificationId"] = notificationId,
                    ["actionId"] = actionId ?? "",
                    ["clickedAt"] = DateTime.UtcNow
                }
            };
            
            await Clients.Caller.SendAsync("NotificationClicked", clickedEvent);
            
            _logger.LogDebug("Notification {NotificationId} clicked by user {UserId} with action {ActionId}", notificationId, userId, actionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking notification {NotificationId} as clicked for user {UserId}", notificationId, userId);
            await Clients.Caller.SendAsync("Error", new { message = "Failed to mark notification as clicked", notificationId });
        }
    }

    public async Task DismissNotification(string notificationId)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId)) return;

        try
        {
            await _notificationService.DismissNotificationAsync(notificationId, userId);
            
            var dismissedEvent = new RealTimeNotificationEvent
            {
                EventId = Guid.NewGuid().ToString(),
                UserId = userId,
                EventType = RealTimeEventType.NotificationDismissed,
                EventData = new Dictionary<string, object>
                {
                    ["notificationId"] = notificationId,
                    ["dismissedAt"] = DateTime.UtcNow
                }
            };
            
            await Clients.Caller.SendAsync("NotificationDismissed", dismissedEvent);
            
            _logger.LogDebug("Notification {NotificationId} dismissed by user {UserId}", notificationId, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error dismissing notification {NotificationId} for user {UserId}", notificationId, userId);
            await Clients.Caller.SendAsync("Error", new { message = "Failed to dismiss notification", notificationId });
        }
    }

    public async Task JoinGroup(string groupName)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId)) return;

        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogDebug("User {UserId} joined group {GroupName}", userId, groupName);
    }

    public async Task LeaveGroup(string groupName)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId)) return;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _logger.LogDebug("User {UserId} left group {GroupName}", userId, groupName);
    }

    public async Task UpdatePreferences(UpdateNotificationPreferencesRequest request)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId)) return;

        try
        {
            await _notificationService.UpdatePreferencesAsync(userId, request);
            
            var preferencesEvent = new RealTimeNotificationEvent
            {
                EventId = Guid.NewGuid().ToString(),
                UserId = userId,
                EventType = RealTimeEventType.PreferencesUpdated,
                EventData = new Dictionary<string, object>
                {
                    ["updatedAt"] = DateTime.UtcNow
                }
            };
            
            await Clients.Caller.SendAsync("PreferencesUpdated", preferencesEvent);
            
            _logger.LogDebug("Notification preferences updated for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating preferences for user {UserId}", userId);
            await Clients.Caller.SendAsync("Error", new { message = "Failed to update preferences" });
        }
    }

    private async Task SendPendingNotifications(string userId)
    {
        try
        {
            var pendingNotifications = await _notificationService.GetPendingNotificationsAsync(userId);
            
            foreach (var notification in pendingNotifications)
            {
                var notificationEvent = new RealTimeNotificationEvent
                {
                    EventId = Guid.NewGuid().ToString(),
                    UserId = userId,
                    Notification = notification,
                    EventType = RealTimeEventType.NotificationCreated
                };
                
                await Clients.Caller.SendAsync("NotificationReceived", notificationEvent);
            }
            
            if (pendingNotifications.Any())
            {
                _logger.LogInformation("Sent {Count} pending notifications to user {UserId}", pendingNotifications.Count(), userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending pending notifications to user {UserId}", userId);
        }
    }

    private string GetUserId()
    {
        return Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? 
               Context.User?.FindFirst("sub")?.Value ?? 
               Context.User?.FindFirst("user_id")?.Value ?? 
               string.Empty;
    }
}