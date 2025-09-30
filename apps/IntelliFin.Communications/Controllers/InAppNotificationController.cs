using IntelliFin.Communications.Models;
using IntelliFin.Communications.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace IntelliFin.Communications.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InAppNotificationController : ControllerBase
{
    private readonly IInAppNotificationService _notificationService;
    private readonly ILogger<InAppNotificationController> _logger;

    public InAppNotificationController(
        IInAppNotificationService notificationService,
        ILogger<InAppNotificationController> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>
    /// Send an in-app notification to a specific user
    /// </summary>
    [HttpPost("send")]
    public async Task<IActionResult> SendNotification([FromBody] CreateInAppNotificationRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var response = await _notificationService.SendNotificationAsync(request);
            
            if (response.Success)
            {
                return Ok(response);
            }
            
            return BadRequest(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending in-app notification");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Send bulk in-app notifications to multiple users
    /// </summary>
    [HttpPost("send-bulk")]
    public async Task<IActionResult> SendBulkNotification([FromBody] BulkInAppNotificationRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var response = await _notificationService.SendBulkNotificationAsync(request);
            
            if (response.Success)
            {
                return Ok(response);
            }
            
            return BadRequest(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending bulk in-app notification");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Get notifications for the current user
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetNotifications([FromQuery] GetNotificationsRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var response = await _notificationService.GetNotificationsAsync(userId, request);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting notifications for user");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Get a specific notification
    /// </summary>
    [HttpGet("{notificationId}")]
    public async Task<IActionResult> GetNotification(string notificationId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var notification = await _notificationService.GetNotificationAsync(notificationId, userId);
            
            if (notification == null)
            {
                return NotFound();
            }
            
            return Ok(notification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting notification {NotificationId}", notificationId);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Get unread notification count for the current user
    /// </summary>
    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var count = await _notificationService.GetUnreadCountAsync(userId);
            return Ok(new { unreadCount = count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unread count for user");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Mark a notification as read
    /// </summary>
    [HttpPost("{notificationId}/mark-read")]
    public async Task<IActionResult> MarkAsRead(string notificationId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            await _notificationService.MarkAsReadAsync(notificationId, userId);
            return Ok(new { message = "Notification marked as read" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking notification {NotificationId} as read", notificationId);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Mark a notification as clicked
    /// </summary>
    [HttpPost("{notificationId}/mark-clicked")]
    public async Task<IActionResult> MarkAsClicked(string notificationId, [FromBody] MarkClickedRequest? request = null)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            await _notificationService.MarkAsClickedAsync(notificationId, userId, request?.ActionId);
            return Ok(new { message = "Notification marked as clicked" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking notification {NotificationId} as clicked", notificationId);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Dismiss a notification
    /// </summary>
    [HttpPost("{notificationId}/dismiss")]
    public async Task<IActionResult> DismissNotification(string notificationId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            await _notificationService.DismissNotificationAsync(notificationId, userId);
            return Ok(new { message = "Notification dismissed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error dismissing notification {NotificationId}", notificationId);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Mark all notifications as read for the current user
    /// </summary>
    [HttpPost("mark-all-read")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            await _notificationService.MarkAllAsReadAsync(userId);
            return Ok(new { message = "All notifications marked as read" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking all notifications as read for user");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Delete a notification
    /// </summary>
    [HttpDelete("{notificationId}")]
    public async Task<IActionResult> DeleteNotification(string notificationId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            await _notificationService.DeleteNotificationAsync(notificationId, userId);
            return Ok(new { message = "Notification deleted" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting notification {NotificationId}", notificationId);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Get notification preferences for the current user
    /// </summary>
    [HttpGet("preferences")]
    public async Task<IActionResult> GetPreferences()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var preferences = await _notificationService.GetPreferencesAsync(userId);
            return Ok(preferences);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting notification preferences for user");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Update notification preferences for the current user
    /// </summary>
    [HttpPut("preferences")]
    public async Task<IActionResult> UpdatePreferences([FromBody] UpdateNotificationPreferencesRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            await _notificationService.UpdatePreferencesAsync(userId, request);
            return Ok(new { message = "Preferences updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating notification preferences for user");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Get engagement metrics for the current user
    /// </summary>
    [HttpGet("engagement-metrics")]
    public async Task<IActionResult> GetEngagementMetrics([FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var start = startDate ?? DateTime.UtcNow.AddDays(-30);
            var end = endDate ?? DateTime.UtcNow;

            var metrics = await _notificationService.GetEngagementMetricsAsync(userId, start, end);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting engagement metrics for user");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Get pending notifications for the current user (for reconnection scenarios)
    /// </summary>
    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingNotifications()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var notifications = await _notificationService.GetPendingNotificationsAsync(userId);
            return Ok(notifications);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending notifications for user");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    private string GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? 
               User.FindFirst("sub")?.Value ?? 
               User.FindFirst("user_id")?.Value ?? 
               string.Empty;
    }
}

public class MarkClickedRequest
{
    public string? ActionId { get; set; }
}