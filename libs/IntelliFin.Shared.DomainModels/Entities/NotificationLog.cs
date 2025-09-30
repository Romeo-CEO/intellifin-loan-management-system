using System.ComponentModel.DataAnnotations;

namespace IntelliFin.Shared.DomainModels.Entities;

public class NotificationLog
{
    public long Id { get; set; }

    [Required]
    public Guid EventId { get; set; }

    [Required]
    [MaxLength(100)]
    public string RecipientId { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string RecipientType { get; set; } = string.Empty; // Customer, LoanOfficer, etc.

    [Required]
    [MaxLength(20)]
    public string Channel { get; set; } = string.Empty; // SMS, Email, InApp, Push

    public int? TemplateId { get; set; }

    [MaxLength(500)]
    public string? Subject { get; set; }

    [Required]
    public string Content { get; set; } = string.Empty;

    public string? PersonalizationData { get; set; } // JSON

    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;

    public string? GatewayResponse { get; set; } // JSON

    [Required]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? SentAt { get; set; }

    public DateTimeOffset? DeliveredAt { get; set; }

    [MaxLength(1000)]
    public string? FailureReason { get; set; }

    public int RetryCount { get; set; } = 0;

    public int MaxRetries { get; set; } = 3;

    public decimal? Cost { get; set; }

    [MaxLength(100)]
    public string? ExternalId { get; set; } // Provider's message ID

    [Required]
    public int BranchId { get; set; }

    [Required]
    [MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    // Navigation Properties
    public NotificationTemplate? Template { get; set; }
}

public enum NotificationStatus
{
    Pending = 0,
    Queued = 1,
    Sent = 2,
    Delivered = 3,
    Failed = 4,
    Expired = 5
}
