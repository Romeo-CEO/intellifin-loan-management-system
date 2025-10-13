namespace IntelliFin.AdminService.Models;

public sealed class AuditEventFilter
{
    public DateTime StartDate { get; set; } = DateTime.UtcNow.AddDays(-30);
    public DateTime EndDate { get; set; } = DateTime.UtcNow;
    public string? Actor { get; set; }
    public string? Action { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? CorrelationId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 100;
}

public sealed class AuditEventPage
{
    public required IReadOnlyList<AuditEvent> Events { get; init; }
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public sealed class AuditBufferMetrics
{
    public int BufferedEvents { get; init; }
    public int BatchSize { get; init; }
    public int MaxBufferSize { get; init; }
    public DateTime LastFlushUtc { get; init; }
}
