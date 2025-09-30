# Epic 5: Real-time Staff Notifications Enhancement

## Overview
Enhance the existing SignalR real-time notification infrastructure to support advanced staff notification workflows, role-based routing, notification state management, and actionable notifications for operational efficiency.

## Current State Analysis
**Existing Infrastructure:**
- SignalR hub fully operational with connection management
- User groups and real-time event delivery implemented
- Basic in-app notification foundation exists
- Connection management and user tracking operational

**Enhancement Required:**
- Advanced role-based notification routing
- Notification state management (read/unread/dismissed)
- Actionable notifications with workflow integration
- Enhanced connection management for offline users

## User Stories

### Story 5.1: Role-Based Notification Routing
**As a** branch manager
**I want** real-time dashboard notifications for urgent loan approvals
**So that** I can prioritize high-value decisions

**Acceptance Criteria:**
- ✅ Role-based notification routing (LoanOfficer, Underwriter, Manager, etc.)
- ✅ Priority-based notification delivery
- ✅ Branch-specific notification filtering
- ✅ Escalation workflows for unattended notifications
- ✅ Real-time delivery to active users

### Story 5.2: System Monitoring Alerts
**As a** system administrator
**I want** immediate alerts for communication system failures
**So that** I can maintain service reliability

**Acceptance Criteria:**
- ✅ Real-time system health notifications
- ✅ SMS provider failure alerts
- ✅ Database connectivity alerts
- ✅ Performance threshold breach notifications
- ✅ Automated escalation to on-call staff

### Story 5.3: Interactive Notification Actions
**As a** loan officer
**I want** to approve or review loans directly from notifications
**So that** I can act quickly without navigating multiple screens

**Acceptance Criteria:**
- ✅ Actionable notification buttons (Approve, Review, Dismiss)
- ✅ Workflow integration with Camunda processes
- ✅ Contextual information display
- ✅ Action confirmation and audit logging
- ✅ Real-time status updates to all relevant staff

## Technical Implementation

### Enhanced SignalR Hub
```csharp
public class EnhancedNotificationHub : Hub
{
    private readonly INotificationService _notificationService;
    private readonly IUserContextService _userContext;
    private readonly INotificationRepository _repository;

    public async Task JoinRoleGroup(string role, string branchId)
    {
        var groupName = $"{role}_{branchId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        // Send queued notifications for offline user
        await SendQueuedNotifications();
    }

    public async Task MarkAsRead(int notificationId)
    {
        await _repository.MarkAsReadAsync(notificationId, Context.UserIdentifier);

        // Notify other sessions of the same user
        await Clients.User(Context.UserIdentifier)
            .SendAsync("NotificationRead", notificationId);
    }

    public async Task ExecuteAction(int notificationId, string action, object? data = null)
    {
        var notification = await _repository.GetByIdAsync(notificationId);
        if (notification?.Actions?.ContainsKey(action) == true)
        {
            await _notificationService.ExecuteActionAsync(notification, action, data);

            // Update notification status
            await _repository.UpdateStatusAsync(notificationId, NotificationStatus.Actioned);

            // Notify relevant users
            await NotifyActionExecuted(notification, action);
        }
    }
}
```

### Notification State Management
```csharp
public class InAppNotification
{
    public int Id { get; set; }
    public string UserId { get; set; }
    public string Title { get; set; }
    public string Message { get; set; }
    public NotificationType Type { get; set; }
    public NotificationPriority Priority { get; set; }
    public string? ActionUrl { get; set; }
    public Dictionary<string, object>? Actions { get; set; } // JSON
    public object? Metadata { get; set; } // JSON
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? Category { get; set; }
    public string? BranchId { get; set; }
}

public enum NotificationType
{
    Info,
    Warning,
    Error,
    Success,
    Action, // Requires user action
    Alert,  // System alert
    Workflow // Workflow-related
}

public enum NotificationPriority
{
    Low = 1,
    Normal = 2,
    High = 3,
    Urgent = 4,
    Critical = 5
}
```

### Role-Based Routing Configuration
```json
{
  "NotificationRouting": {
    "LoanApplication": {
      "Created": [
        {"Role": "LoanOfficer", "Branch": "{{application.branchId}}", "Priority": "Normal"},
        {"Role": "BranchManager", "Branch": "{{application.branchId}}", "Priority": "Low"}
      ],
      "HighValue": [
        {"Role": "LoanOfficer", "Branch": "{{application.branchId}}", "Priority": "High"},
        {"Role": "BranchManager", "Branch": "{{application.branchId}}", "Priority": "High"},
        {"Role": "RegionalManager", "Region": "{{branch.regionId}}", "Priority": "Normal"}
      ]
    },
    "SystemAlert": {
      "DatabaseDown": [
        {"Role": "SystemAdmin", "Priority": "Critical"},
        {"Role": "ITSupport", "Priority": "Critical"}
      ],
      "PerformanceIssue": [
        {"Role": "SystemAdmin", "Priority": "High"},
        {"Role": "BranchManager", "Branch": "*", "Priority": "Normal"}
      ]
    }
  }
}
```

### Actionable Notifications
```csharp
public class LoanApprovalNotification
{
    public string Title => $"Loan Approval Required - {LoanReference}";
    public string Message => $"Loan application for {CustomerName} requires approval (K {Amount:N2})";

    public Dictionary<string, object> Actions => new()
    {
        ["approve"] = new { Label = "Approve", Style = "success", RequiresComment = false },
        ["request_info"] = new { Label = "Request Info", Style = "warning", RequiresComment = true },
        ["decline"] = new { Label = "Decline", Style = "danger", RequiresComment = true },
        ["view_details"] = new { Label = "View Details", Style = "primary", Url = $"/loans/{LoanId}" }
    };

    public object Metadata => new
    {
        LoanId,
        LoanReference,
        CustomerName,
        Amount,
        ApplicationDate,
        ProductType,
        RequiredApprovalLevel = GetRequiredApprovalLevel()
    };
}
```

### Offline User Management
```csharp
public class NotificationConnectionManager
{
    private readonly IMemoryCache _connections;
    private readonly INotificationRepository _repository;

    public async Task<bool> IsUserOnlineAsync(string userId)
    {
        return _connections.TryGetValue($"user_{userId}", out _);
    }

    public async Task QueueNotificationAsync(string userId, InAppNotification notification)
    {
        if (await IsUserOnlineAsync(userId))
        {
            await SendImmediatelyAsync(userId, notification);
        }
        else
        {
            await _repository.QueueForOfflineUserAsync(userId, notification);
        }
    }

    public async Task SendQueuedNotificationsAsync(string userId)
    {
        var queued = await _repository.GetQueuedNotificationsAsync(userId);
        foreach (var notification in queued)
        {
            await SendImmediatelyAsync(userId, notification);
        }
        await _repository.ClearQueuedNotificationsAsync(userId);
    }
}
```

### System Health Monitoring
```csharp
public class SystemHealthNotificationService : BackgroundService
{
    private readonly INotificationService _notificationService;
    private readonly IHealthCheckService _healthCheck;
    private readonly ILogger<SystemHealthNotificationService> _logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var healthReport = await _healthCheck.CheckHealthAsync(stoppingToken);

            foreach (var check in healthReport.Entries)
            {
                if (check.Value.Status == HealthStatus.Unhealthy)
                {
                    await _notificationService.SendSystemAlertAsync(new SystemAlert
                    {
                        Type = "HealthCheckFailure",
                        Severity = AlertSeverity.Critical,
                        Component = check.Key,
                        Message = check.Value.Description,
                        Exception = check.Value.Exception?.ToString()
                    });
                }
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
```

### Enhanced Notification API
```http
# Get user notifications with filtering
GET /api/notifications?unreadOnly=true&category=LoanApproval&priority=High&page=1&pageSize=20

# Mark notification as read
PUT /api/notifications/{id}/read

# Execute notification action
POST /api/notifications/{id}/actions/{actionName}
Content-Type: application/json
{
  "comment": "Approved with standard terms",
  "metadata": { "approvalLevel": "L1" }
}

# Get notification statistics
GET /api/notifications/stats
{
  "unreadCount": 5,
  "highPriorityCount": 2,
  "actionRequiredCount": 3,
  "categories": {
    "LoanApproval": 3,
    "SystemAlert": 1,
    "Collections": 1
  }
}
```

### Real-time Event Examples

#### Loan Application Created
```javascript
// Frontend receives real-time notification
signalR.on("NotificationReceived", (notification) => {
    if (notification.type === "LoanApplication" && notification.priority === "High") {
        showUrgentNotification({
            title: notification.title,
            message: notification.message,
            actions: notification.actions,
            metadata: notification.metadata
        });
    }
});

// User takes action
function approveApplication(notificationId, loanId) {
    signalR.invoke("ExecuteAction", notificationId, "approve", {
        comment: "Standard approval",
        loanId: loanId
    });
}
```

#### System Alert Escalation
```csharp
public async Task HandleSystemAlert(SystemAlert alert)
{
    // Send to primary recipients
    await SendToRole("SystemAdmin", alert.ToNotification(NotificationPriority.Critical));

    // Escalate if not acknowledged within 5 minutes
    _ = Task.Delay(TimeSpan.FromMinutes(5))
        .ContinueWith(async _ =>
        {
            if (!await IsAlertAcknowledged(alert.Id))
            {
                await SendToRole("ITManager", alert.ToNotification(NotificationPriority.Critical));
                await SendSmsAlert(alert); // Backup SMS notification
            }
        });
}
```

## Success Metrics
- **Real-time Delivery**: <3 seconds from event to notification display
- **Notification Action Rate**: >80% of actionable notifications acted upon
- **System Alert Response**: <5 minutes average response time to critical alerts
- **User Engagement**: >90% of staff using in-app notifications actively
- **Offline Queue Success**: 100% delivery of queued notifications on reconnection

## Integration Points

### Workflow Integration (Camunda)
- Notification actions trigger workflow processes
- Workflow status updates generate notifications
- Human task assignments create actionable notifications

### Audit Trail Integration
- All notification actions logged to audit system
- User interaction tracking for compliance
- Performance metrics collection

### Mobile Responsiveness
- Tablet-optimized notification interface
- Touch-friendly action buttons
- Offline notification queuing for mobile users

## Risk Mitigation
- **Connection Failures**: Automatic reconnection with exponential backoff
- **Message Loss**: Persistent storage for critical notifications
- **Performance Issues**: Connection pooling and message batching
- **Spam Prevention**: Rate limiting and priority-based throttling
- **Security**: Role validation and action authorization

## Dependencies
- Existing SignalR infrastructure
- Epic 3: Database persistence for notification storage
- User authentication and role management
- Camunda workflow integration