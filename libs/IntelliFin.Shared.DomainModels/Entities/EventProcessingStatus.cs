using System.ComponentModel.DataAnnotations;

namespace IntelliFin.Shared.DomainModels.Entities;

public class EventProcessingStatus
{
    public long Id { get; set; }

    [Required]
    public Guid EventId { get; set; }

    [Required]
    [MaxLength(100)]
    public string EventType { get; set; } = string.Empty;

    [Required]
    public DateTimeOffset ProcessedAt { get; set; } = DateTimeOffset.UtcNow;

    [Required]
    [MaxLength(20)]
    public string ProcessingResult { get; set; } = string.Empty; // Success, Failed, Skipped

    public string? ErrorDetails { get; set; }
}
