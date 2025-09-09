using System.ComponentModel.DataAnnotations;

namespace IntelliFin.Communications.Models;

// Enums are defined in SmsModels.cs

public class SmsNotificationRequest
{
    [Required]
    public string PhoneNumber { get; set; } = string.Empty;
    
    [Required]
    public string Message { get; set; } = string.Empty;
    
    public SmsNotificationType NotificationType { get; set; }
    
    public string? ClientId { get; set; }
    
    public string? LoanId { get; set; }
    
    public string? PaymentId { get; set; }
    
    public Dictionary<string, object> TemplateData { get; set; } = new();
    
    public int Priority { get; set; } = 1; // 1 = High, 2 = Normal, 3 = Low
    
    public DateTime? ScheduledTime { get; set; }
    
    public SmsProvider PreferredProvider { get; set; } = SmsProvider.Airtel;
}

public class SmsNotificationResponse
{
    public string NotificationId { get; set; } = string.Empty;
    
    public SmsDeliveryStatus Status { get; set; }
    
    public string? ProviderMessageId { get; set; }
    
    public SmsProvider UsedProvider { get; set; }
    
    public DateTime SentAt { get; set; }
    
    public string? ErrorMessage { get; set; }
    
    public decimal Cost { get; set; }
    
    public int RetryCount { get; set; }
}

// SmsTemplate and SmsDeliveryReport are defined in SmsModels.cs

public class SmsProviderSettings
{
    public string ApiUrl { get; set; } = string.Empty;
    
    public string ApiKey { get; set; } = string.Empty;
    
    public string Username { get; set; } = string.Empty;
    
    public string Password { get; set; } = string.Empty;
    
    public string SenderId { get; set; } = "IntelliFin";
    
    public decimal CostPerSms { get; set; }
    
    public int TimeoutSeconds { get; set; } = 30;
    
    public bool IsActive { get; set; } = true;
    
    public int MaxRetries { get; set; } = 3;
}

// SmsAnalytics is defined in SmsModels.cs - we'll extend it if needed

public class SmsRateLimitConfig
{
    public int MaxSmsPerMinute { get; set; } = 60;
    
    public int MaxSmsPerHour { get; set; } = 1000;
    
    public int MaxSmsPerDay { get; set; } = 10000;
    
    public decimal DailyCostLimit { get; set; } = 1000m; // ZMW
}

public class SmsOptOutRequest
{
    [Required]
    public string PhoneNumber { get; set; } = string.Empty;
    
    public List<SmsNotificationType>? NotificationTypes { get; set; }
    
    public string? Reason { get; set; }
    
    public DateTime OptOutDate { get; set; } = DateTime.UtcNow;
}

public class SmsOptOutStatus
{
    public string PhoneNumber { get; set; } = string.Empty;
    
    public bool IsOptedOut { get; set; }
    
    public List<SmsNotificationType> OptedOutTypes { get; set; } = new();
    
    public DateTime? OptOutDate { get; set; }
    
    public string? OptOutReason { get; set; }
}

// BulkSmsRequest and BulkSmsResponse are defined in SmsModels.cs