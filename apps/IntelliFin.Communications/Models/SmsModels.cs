using System.ComponentModel.DataAnnotations;

namespace IntelliFin.Communications.Models;

public class SmsMessage
{
    public string Id { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string TemplateId { get; set; } = string.Empty;
    public Dictionary<string, string> TemplateParameters { get; set; } = new();
    public SmsStatus Status { get; set; } = SmsStatus.Pending;
    public SmsPriority Priority { get; set; } = SmsPriority.Normal;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ScheduledAt { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public string? ExternalId { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public decimal Cost { get; set; }
    public string Gateway { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class SendSmsRequest
{
    [Required]
    public string To { get; set; } = string.Empty;
    
    public string? From { get; set; }
    
    [Required]
    public string Content { get; set; } = string.Empty;
    
    public string? TemplateId { get; set; }
    public Dictionary<string, string> TemplateParameters { get; set; } = new();
    public SmsPriority Priority { get; set; } = SmsPriority.Normal;
    public DateTime? ScheduledAt { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class SendSmsResponse
{
    public bool Success { get; set; }
    public string MessageId { get; set; } = string.Empty;
    public string? ExternalId { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> Errors { get; set; } = new();
    public decimal EstimatedCost { get; set; }
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}

public class BulkSmsRequest
{
    [Required]
    public List<BulkSmsRecipient> Recipients { get; set; } = new();
    
    public string? From { get; set; }
    
    [Required]
    public string TemplateId { get; set; } = string.Empty;
    
    public SmsPriority Priority { get; set; } = SmsPriority.Normal;
    public DateTime? ScheduledAt { get; set; }
    public Dictionary<string, string> GlobalMetadata { get; set; } = new();
    public bool EnableDeduplication { get; set; } = true;
    public int BatchSize { get; set; } = 100;
}

public class BulkSmsRecipient
{
    [Required]
    public string To { get; set; } = string.Empty;
    public Dictionary<string, string> TemplateParameters { get; set; } = new();
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class BulkSmsResponse
{
    public bool Success { get; set; }
    public string BatchId { get; set; } = string.Empty;
    public int TotalRecipients { get; set; }
    public int AcceptedRecipients { get; set; }
    public int RejectedRecipients { get; set; }
    public List<string> MessageIds { get; set; } = new();
    public List<BulkSmsError> Errors { get; set; } = new();
    public decimal TotalEstimatedCost { get; set; }
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}

public class BulkSmsError
{
    public string Recipient { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

public class SmsTemplate
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public List<string> Parameters { get; set; } = new();
    public SmsCategory Category { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastModified { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class CreateSmsTemplateRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;
    
    public string Description { get; set; } = string.Empty;
    
    [Required]
    public string Content { get; set; } = string.Empty;
    
    public SmsCategory Category { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class SmsDeliveryReport
{
    public string MessageId { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public SmsStatus Status { get; set; }
    public DateTime StatusUpdatedAt { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public decimal ActualCost { get; set; }
    public string Gateway { get; set; } = string.Empty;
}

public class SmsAnalytics
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalSent { get; set; }
    public int TotalDelivered { get; set; }
    public int TotalFailed { get; set; }
    public double DeliveryRate { get; set; }
    public decimal TotalCost { get; set; }
    public decimal AverageCostPerMessage { get; set; }
    public Dictionary<SmsStatus, int> StatusBreakdown { get; set; } = new();
    public Dictionary<string, int> GatewayBreakdown { get; set; } = new();
    public Dictionary<SmsCategory, int> CategoryBreakdown { get; set; } = new();
    public List<SmsVolumeByHour> HourlyVolume { get; set; } = new();
}

public class SmsVolumeByHour
{
    public int Hour { get; set; }
    public int Count { get; set; }
    public double DeliveryRate { get; set; }
}

public class SmsGatewayConfig
{
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public int Priority { get; set; }
    public decimal CostPerMessage { get; set; }
    public int DailyLimit { get; set; }
    public int MonthlyLimit { get; set; }
    public Dictionary<string, string> Settings { get; set; } = new();
    public List<string> SupportedCountries { get; set; } = new();
}

public class SmsQuotaInfo
{
    public int DailyLimit { get; set; }
    public int DailyUsed { get; set; }
    public int DailyRemaining { get; set; }
    public int MonthlyLimit { get; set; }
    public int MonthlyUsed { get; set; }
    public int MonthlyRemaining { get; set; }
    public decimal MonthlyBudget { get; set; }
    public decimal MonthlySpent { get; set; }
    public decimal MonthlyBudgetRemaining { get; set; }
    public DateTime ResetDate { get; set; }
}

public enum SmsStatus
{
    Pending,
    Queued,
    Sent,
    Delivered,
    Failed,
    Cancelled,
    Expired,
    Unknown
}

public enum SmsPriority
{
    Low = 1,
    Normal = 2,
    High = 3,
    Critical = 4
}

public enum SmsCategory
{
    Transactional,
    Marketing,
    Alert,
    Reminder,
    OTP,
    System,
    Promotional
}

// Extended enums for SMS Notification System
public enum SmsNotificationType
{
    LoanApplicationStatus,
    PaymentReminder,
    PaymentConfirmation,
    OverduePayment,
    LoanApproval,
    LoanDisbursement,
    PmecDeductionStatus,
    AccountBalance,
    TransactionAlert,
    GeneralNotification
}

public enum SmsDeliveryStatus
{
    Pending,
    Sent,
    Delivered,
    Failed,
    Retry,
    OptedOut
}

public enum SmsProvider
{
    Airtel,
    MTN,
    Zamtel
}