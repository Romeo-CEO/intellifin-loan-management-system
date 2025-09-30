using System.ComponentModel.DataAnnotations;

namespace IntelliFin.Shared.DomainModels.Entities;

public class NotificationTemplate
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Category { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Channel { get; set; } = string.Empty; // SMS, Email, InApp, Push

    [Required]
    [MaxLength(10)]
    public string Language { get; set; } = "en";

    [MaxLength(500)]
    public string? Subject { get; set; }

    [Required]
    public string Content { get; set; } = string.Empty;

    public string? PersonalizationTokens { get; set; } // JSON array

    public bool IsActive { get; set; } = true;

    [Required]
    [MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    [Required]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [MaxLength(100)]
    public string? UpdatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    [Required]
    public int Version { get; set; } = 1;

    // Navigation Properties
    public List<NotificationLog> NotificationLogs { get; set; } = new();
}
