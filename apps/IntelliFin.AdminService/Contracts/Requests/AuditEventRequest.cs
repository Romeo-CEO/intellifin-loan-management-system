using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace IntelliFin.AdminService.Contracts.Requests;

public sealed class AuditEventRequest
{
    [Required]
    public DateTime Timestamp { get; set; }

    [Required]
    [MaxLength(100)]
    public string Actor { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Action { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? EntityType { get; set; }

    [MaxLength(100)]
    public string? EntityId { get; set; }

    [MaxLength(100)]
    public string? CorrelationId { get; set; }

    [MaxLength(45)]
    public string? IpAddress { get; set; }

    [MaxLength(500)]
    public string? UserAgent { get; set; }

    public JsonElement? EventData { get; set; }
}

public sealed class AuditEventBatchRequest
{
    private const int MaxBatchSize = 1000;

    [MinLength(1)]
    [MaxLength(MaxBatchSize)]
    public List<AuditEventRequest> Events { get; set; } = new();
}

public sealed class AuditQueryRequest
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Actor { get; set; }
    public string? Action { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? CorrelationId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 100;
}
