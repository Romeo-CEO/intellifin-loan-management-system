namespace IntelliFin.ClientManagement.Models;

/// <summary>
/// Request for sending a notification
/// </summary>
public class NotificationRequest
{
    /// <summary>
    /// Template ID for the notification
    /// </summary>
    public string TemplateId { get; set; } = string.Empty;

    /// <summary>
    /// Recipient client ID
    /// </summary>
    public string RecipientId { get; set; } = string.Empty;

    /// <summary>
    /// Notification channel (SMS, Email)
    /// </summary>
    public string Channel { get; set; } = "SMS";

    /// <summary>
    /// Template personalization data
    /// </summary>
    public Dictionary<string, object> PersonalizationData { get; set; } = new();

    /// <summary>
    /// Whether this is a critical notification (bypasses consent)
    /// </summary>
    public bool IsCritical { get; set; }

    /// <summary>
    /// Reason for consent bypass (if critical)
    /// </summary>
    public string? BypassReason { get; set; }

    /// <summary>
    /// Correlation ID for tracking
    /// </summary>
    public string? CorrelationId { get; set; }
}

/// <summary>
/// Result of notification attempt
/// </summary>
public class NotificationResult
{
    /// <summary>
    /// Whether notification was sent successfully
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Number of attempts made
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// Final attempt timestamp
    /// </summary>
    public DateTime FinalAttemptAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Final error message if failed
    /// </summary>
    public string? FinalError { get; set; }

    /// <summary>
    /// Whether message was sent to DLQ
    /// </summary>
    public bool SentToDlq { get; set; }

    /// <summary>
    /// Reason notification was blocked (e.g., "No consent")
    /// </summary>
    public string? BlockedReason { get; set; }
}

/// <summary>
/// Personalization data for notification templates
/// </summary>
public class PersonalizationData
{
    /// <summary>
    /// Client's full name
    /// </summary>
    public string ClientName { get; set; } = string.Empty;

    /// <summary>
    /// Completion date (formatted)
    /// </summary>
    public string? CompletionDate { get; set; }

    /// <summary>
    /// Rejection date (formatted)
    /// </summary>
    public string? RejectionDate { get; set; }

    /// <summary>
    /// Rejection reason
    /// </summary>
    public string? RejectionReason { get; set; }

    /// <summary>
    /// Expected timeframe for EDD
    /// </summary>
    public string? ExpectedTimeframe { get; set; }

    /// <summary>
    /// Branch contact information
    /// </summary>
    public string? BranchContact { get; set; }

    /// <summary>
    /// Contact information for inquiries
    /// </summary>
    public string? ContactInformation { get; set; }

    /// <summary>
    /// Application ID for reference
    /// </summary>
    public string? ApplicationId { get; set; }

    /// <summary>
    /// Risk acceptance level (for EDD approved)
    /// </summary>
    public string? RiskAcceptanceLevel { get; set; }

    /// <summary>
    /// Custom fields for additional personalization
    /// </summary>
    public Dictionary<string, object> CustomFields { get; set; } = new();
}
