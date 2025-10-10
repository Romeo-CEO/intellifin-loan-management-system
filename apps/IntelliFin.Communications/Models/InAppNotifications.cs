namespace IntelliFin.Communications.Models;

/// <summary>
/// Represents an in-app notification creation request.
/// </summary>
public class CreateInAppNotificationRequest
{
    public string UserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Link { get; set; }
    public InAppNotificationCategory Category { get; set; } = InAppNotificationCategory.General;
    public InAppNotificationType Type { get; set; } = InAppNotificationType.Info;
    public InAppNotificationPriority Priority { get; set; } = InAppNotificationPriority.Normal;
    public string? SourceId { get; set; }
    public string? SourceType { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Response returned when an in-app notification is dispatched.
/// </summary>
public class InAppNotificationResponse
{
    public bool Success { get; set; }
    public string? NotificationId { get; set; }
    public string? ErrorMessage { get; set; }
}

public enum InAppNotificationPriority
{
    Low,
    Normal,
    High,
    Critical
}

public enum InAppNotificationType
{
    Info,
    Success,
    Warning,
    Error
}

public enum InAppNotificationCategory
{
    General,
    LoanApplication,
    Risk,
    System,
    CustomerSupport
}
