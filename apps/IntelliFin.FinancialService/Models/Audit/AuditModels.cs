using System.Text.Json.Serialization;

namespace IntelliFin.FinancialService.Models.Audit;

public sealed class AuditEventQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public string? Actor { get; set; }
    public string? Action { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public sealed class AuditEventPageResponse
{
    [JsonPropertyName("data")]
    public required IReadOnlyList<AuditEventDto> Data { get; init; }

    [JsonPropertyName("pagination")]
    public required PaginationMetadataDto Pagination { get; init; }
}

public sealed class AuditEventDto
{
    [JsonPropertyName("eventId")]
    public Guid EventId { get; init; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; }

    [JsonPropertyName("actor")]
    public string Actor { get; init; } = string.Empty;

    [JsonPropertyName("action")]
    public string Action { get; init; } = string.Empty;

    [JsonPropertyName("entityType")]
    public string? EntityType { get; init; }

    [JsonPropertyName("entityId")]
    public string? EntityId { get; init; }

    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; init; }

    [JsonPropertyName("ipAddress")]
    public string? IpAddress { get; init; }

    [JsonPropertyName("userAgent")]
    public string? UserAgent { get; init; }

    [JsonPropertyName("eventData")]
    public string? EventData { get; init; }

    [JsonPropertyName("currentEventHash")]
    public string? CurrentEventHash { get; init; }

    [JsonPropertyName("previousEventHash")]
    public string? PreviousEventHash { get; init; }

    [JsonPropertyName("integrityStatus")]
    public string IntegrityStatus { get; init; } = string.Empty;
}

public sealed class PaginationMetadataDto
{
    [JsonPropertyName("currentPage")]
    public int CurrentPage { get; init; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; init; }

    [JsonPropertyName("totalPages")]
    public int TotalPages { get; init; }

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; init; }
}

public sealed record AuditExportResult(byte[] Content, string ContentType, string FileName);

public sealed class AuditIntegrityStatusResponse
{
    [JsonPropertyName("lastVerification")]
    public AuditVerificationSummary? LastVerification { get; init; }

    [JsonPropertyName("chainStatus")]
    public required AuditChainStatus ChainStatus { get; init; }
}

public sealed class AuditVerificationSummary
{
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTime? Timestamp { get; init; }

    [JsonPropertyName("eventsVerified")]
    public long EventsVerified { get; init; }

    [JsonPropertyName("durationMs")]
    public long DurationMs { get; init; }

    [JsonPropertyName("initiatedBy")]
    public string? InitiatedBy { get; init; }
}

public sealed class AuditChainStatus
{
    [JsonPropertyName("totalEvents")]
    public long TotalEvents { get; init; }

    [JsonPropertyName("verifiedEvents")]
    public long VerifiedEvents { get; init; }

    [JsonPropertyName("brokenEvents")]
    public long BrokenEvents { get; init; }

    [JsonPropertyName("coveragePercentage")]
    public double CoveragePercentage { get; init; }
}

public sealed class AuditIntegrityHistoryResponse
{
    [JsonPropertyName("data")]
    public required IReadOnlyList<AuditVerificationHistoryItem> Data { get; init; }

    [JsonPropertyName("pagination")]
    public required PaginationMetadataDto Pagination { get; init; }
}

public sealed class AuditVerificationHistoryItem
{
    [JsonPropertyName("verificationId")]
    public Guid VerificationId { get; init; }

    [JsonPropertyName("chainStatus")]
    public string ChainStatus { get; init; } = string.Empty;

    [JsonPropertyName("eventsVerified")]
    public long EventsVerified { get; init; }

    [JsonPropertyName("startTime")]
    public DateTime StartTime { get; init; }

    [JsonPropertyName("endTime")]
    public DateTime EndTime { get; init; }

    [JsonPropertyName("initiatedBy")]
    public string? InitiatedBy { get; init; }
}
