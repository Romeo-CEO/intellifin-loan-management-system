using System.ComponentModel.DataAnnotations;

namespace IntelliFin.Shared.DomainModels.Entities;

/// <summary>
/// Entity representing a routing rule for business events
/// </summary>
public class EventRoutingRule
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string EventType { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string ConsumerType { get; set; } = string.Empty;

    public int Priority { get; set; } = 1;

    [MaxLength(1000)]
    public string? Conditions { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<EventRoutingLog> RoutingLogs { get; set; } = new List<EventRoutingLog>();
}

/// <summary>
/// Log entity for tracking event routing operations
/// </summary>
public class EventRoutingLog
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid EventId { get; set; }

    [Required]
    [MaxLength(100)]
    public string EventType { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string SourceService { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Destinations { get; set; } = string.Empty;

    [Required]
    public DateTime RouteTimestamp { get; set; } = DateTime.UtcNow;

    public bool Success { get; set; } = true;

    // Optional correlation data
    public string? CorrelationId { get; set; }

    [MaxLength(500)]
    public string? ErrorMessage { get; set; }

    // Foreign key to routing rule
    public int? RuleId { get; set; }
    public EventRoutingRule? Rule { get; set; }
}
