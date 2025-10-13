using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace IntelliFin.AdminService.Contracts.Requests;

public sealed class OfflineMergeRequest
{
    private const int MaxBatchSize = 10_000;

    [Required]
    [MaxLength(100)]
    public string DeviceId { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string OfflineSessionId { get; set; } = string.Empty;

    [MinLength(1)]
    [MaxLength(MaxBatchSize)]
    public List<OfflineAuditEventRequest> Events { get; set; } = new();
}

public sealed class OfflineAuditEventRequest
{
    [MaxLength(100)]
    public string? EventId { get; set; }

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

    public JsonElement? EventData { get; set; }
}
