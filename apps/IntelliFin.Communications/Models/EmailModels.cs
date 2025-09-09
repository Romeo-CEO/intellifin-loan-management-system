using System.ComponentModel.DataAnnotations;

namespace IntelliFin.Communications.Models;

public class EmailMessage
{
    public string Id { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string? Cc { get; set; }
    public string? Bcc { get; set; }
    public string From { get; set; } = string.Empty;
    public string? ReplyTo { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string? TextContent { get; set; }
    public string? HtmlContent { get; set; }
    public string? TemplateId { get; set; }
    public Dictionary<string, string> TemplateParameters { get; set; } = new();
    public List<EmailAttachment> Attachments { get; set; } = new();
    public EmailStatus Status { get; set; } = EmailStatus.Pending;
    public EmailPriority Priority { get; set; } = EmailPriority.Normal;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ScheduledAt { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? OpenedAt { get; set; }
    public DateTime? ClickedAt { get; set; }
    public string? ExternalId { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public string Gateway { get; set; } = string.Empty;
    public Dictionary<string, string> Headers { get; set; } = new();
    public Dictionary<string, string> Metadata { get; set; } = new();
    public List<EmailEvent> Events { get; set; } = new();
}

public class EmailAttachment
{
    public string Id { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string? ContentId { get; set; } // For inline attachments
    public bool IsInline { get; set; }
}

public class EmailEvent
{
    public string Id { get; set; } = string.Empty;
    public string MessageId { get; set; } = string.Empty;
    public EmailEventType EventType { get; set; }
    public DateTime Timestamp { get; set; }
    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }
    public string? Location { get; set; }
    public Dictionary<string, string> Data { get; set; } = new();
}

public class SendEmailRequest
{
    [Required]
    public string To { get; set; } = string.Empty;
    
    public string? Cc { get; set; }
    public string? Bcc { get; set; }
    public string? From { get; set; }
    public string? ReplyTo { get; set; }
    
    [Required]
    public string Subject { get; set; } = string.Empty;
    
    public string? TextContent { get; set; }
    public string? HtmlContent { get; set; }
    public string? TemplateId { get; set; }
    public Dictionary<string, string> TemplateParameters { get; set; } = new();
    public List<EmailAttachment> Attachments { get; set; } = new();
    public EmailPriority Priority { get; set; } = EmailPriority.Normal;
    public DateTime? ScheduledAt { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new();
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class SendEmailResponse
{
    public bool Success { get; set; }
    public string MessageId { get; set; } = string.Empty;
    public string? ExternalId { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> Errors { get; set; } = new();
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}

public class BulkEmailRequest
{
    [Required]
    public List<BulkEmailRecipient> Recipients { get; set; } = new();
    
    public string? From { get; set; }
    public string? ReplyTo { get; set; }
    
    [Required]
    public string TemplateId { get; set; } = string.Empty;
    
    public EmailPriority Priority { get; set; } = EmailPriority.Normal;
    public DateTime? ScheduledAt { get; set; }
    public Dictionary<string, string> GlobalHeaders { get; set; } = new();
    public Dictionary<string, string> GlobalMetadata { get; set; } = new();
    public bool EnableDeduplication { get; set; } = true;
    public int BatchSize { get; set; } = 50;
    public bool EnableUnsubscribe { get; set; } = true;
}

public class BulkEmailRecipient
{
    [Required]
    public string To { get; set; } = string.Empty;
    public string? Cc { get; set; }
    public string? Bcc { get; set; }
    public Dictionary<string, string> TemplateParameters { get; set; } = new();
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class BulkEmailResponse
{
    public bool Success { get; set; }
    public string BatchId { get; set; } = string.Empty;
    public int TotalRecipients { get; set; }
    public int AcceptedRecipients { get; set; }
    public int RejectedRecipients { get; set; }
    public List<string> MessageIds { get; set; } = new();
    public List<BulkEmailError> Errors { get; set; } = new();
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}

public class BulkEmailError
{
    public string Recipient { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

public class EmailTemplate
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string? TextContent { get; set; }
    public string? HtmlContent { get; set; }
    public List<string> Parameters { get; set; } = new();
    public EmailCategory Category { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastModified { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class CreateEmailTemplateRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;
    
    public string Description { get; set; } = string.Empty;
    
    [Required]
    public string Subject { get; set; } = string.Empty;
    
    public string? TextContent { get; set; }
    public string? HtmlContent { get; set; }
    public EmailCategory Category { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class EmailDeliveryReport
{
    public string MessageId { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public EmailStatus Status { get; set; }
    public DateTime StatusUpdatedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? OpenedAt { get; set; }
    public DateTime? ClickedAt { get; set; }
    public DateTime? BouncedAt { get; set; }
    public DateTime? UnsubscribedAt { get; set; }
    public string? BounceReason { get; set; }
    public string? ErrorMessage { get; set; }
    public string Gateway { get; set; } = string.Empty;
    public List<EmailEvent> Events { get; set; } = new();
}

public class EmailAnalytics
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalSent { get; set; }
    public int TotalDelivered { get; set; }
    public int TotalOpened { get; set; }
    public int TotalClicked { get; set; }
    public int TotalBounced { get; set; }
    public int TotalUnsubscribed { get; set; }
    public double DeliveryRate { get; set; }
    public double OpenRate { get; set; }
    public double ClickRate { get; set; }
    public double BounceRate { get; set; }
    public double UnsubscribeRate { get; set; }
    public Dictionary<EmailStatus, int> StatusBreakdown { get; set; } = new();
    public Dictionary<string, int> GatewayBreakdown { get; set; } = new();
    public Dictionary<EmailCategory, int> CategoryBreakdown { get; set; } = new();
    public List<EmailVolumeByDay> DailyVolume { get; set; } = new();
    public List<EmailEngagementByHour> HourlyEngagement { get; set; } = new();
}

public class EmailVolumeByDay
{
    public DateTime Date { get; set; }
    public int Sent { get; set; }
    public int Delivered { get; set; }
    public int Opened { get; set; }
    public int Clicked { get; set; }
}

public class EmailEngagementByHour
{
    public int Hour { get; set; }
    public int Opened { get; set; }
    public int Clicked { get; set; }
    public double OpenRate { get; set; }
    public double ClickRate { get; set; }
}

public class EmailBounce
{
    public string Id { get; set; } = string.Empty;
    public string MessageId { get; set; } = string.Empty;
    public string EmailAddress { get; set; } = string.Empty;
    public BounceType BounceType { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime BouncedAt { get; set; }
    public string? DiagnosticCode { get; set; }
    public bool IsSuppressionListed { get; set; }
}

public class EmailSuppression
{
    public string Id { get; set; } = string.Empty;
    public string EmailAddress { get; set; } = string.Empty;
    public SuppressionReason Reason { get; set; }
    public DateTime SuppressedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? Notes { get; set; }
}

public class EmailGatewayConfig
{
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public int Priority { get; set; }
    public int DailyLimit { get; set; }
    public int MonthlyLimit { get; set; }
    public Dictionary<string, string> Settings { get; set; } = new();
    public List<string> SupportedFeatures { get; set; } = new();
}

public class UnsubscribeRequest
{
    public string MessageId { get; set; } = string.Empty;
    public string EmailAddress { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public DateTime UnsubscribedAt { get; set; } = DateTime.UtcNow;
}

public enum EmailStatus
{
    Pending,
    Queued,
    Sent,
    Delivered,
    Opened,
    Clicked,
    Bounced,
    Failed,
    Cancelled,
    Unsubscribed,
    Suppressed
}

public enum EmailPriority
{
    Low = 1,
    Normal = 2,
    High = 3,
    Critical = 4
}

public enum EmailCategory
{
    Transactional,
    Marketing,
    Newsletter,
    Alert,
    Reminder,
    Welcome,
    Promotional,
    System
}

public enum EmailEventType
{
    Sent,
    Delivered,
    Opened,
    Clicked,
    Bounced,
    Unsubscribed,
    Failed,
    Deferred
}

public enum BounceType
{
    Hard,
    Soft,
    Complaint,
    Suppression
}

public enum SuppressionReason
{
    Bounce,
    Complaint,
    Unsubscribe,
    Manual,
    GlobalSuppression
}