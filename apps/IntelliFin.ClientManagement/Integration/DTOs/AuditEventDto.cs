using System.Text.Json.Serialization;

namespace IntelliFin.ClientManagement.Integration.DTOs;

/// <summary>
/// DTO for audit events sent to AdminService
/// Matches AdminService schema for audit trail logging
/// </summary>
public class AuditEventDto
{
    /// <summary>
    /// User ID who performed the action (from JWT sub claim)
    /// </summary>
    [JsonPropertyName("actor")]
    public string Actor { get; set; } = string.Empty;

    /// <summary>
    /// Action performed (e.g., ClientCreated, ClientUpdated)
    /// </summary>
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Type of entity affected (e.g., Client, ClientDocument)
    /// </summary>
    [JsonPropertyName("entityType")]
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// GUID of the affected entity
    /// </summary>
    [JsonPropertyName("entityId")]
    public string EntityId { get; set; } = string.Empty;

    /// <summary>
    /// Request correlation ID for distributed tracing
    /// </summary>
    [JsonPropertyName("correlationId")]
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Client IP address
    /// </summary>
    [JsonPropertyName("ipAddress")]
    public string? IpAddress { get; set; }

    /// <summary>
    /// Additional context as JSON
    /// </summary>
    [JsonPropertyName("eventData")]
    public string? EventData { get; set; }

    /// <summary>
    /// UTC timestamp when event occurred
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Previous event hash for chain integrity (optional)
    /// </summary>
    [JsonPropertyName("previousEventHash")]
    public string? PreviousEventHash { get; set; }
}

/// <summary>
/// Response from AdminService for single audit event
/// </summary>
public class AuditEventResponse
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("hash")]
    public string? Hash { get; set; }
}

/// <summary>
/// Response from AdminService for batch audit events
/// </summary>
public class BatchAuditResponse
{
    [JsonPropertyName("processedCount")]
    public int ProcessedCount { get; set; }

    [JsonPropertyName("failedCount")]
    public int FailedCount { get; set; }

    [JsonPropertyName("failedIds")]
    public List<string>? FailedIds { get; set; }
}
