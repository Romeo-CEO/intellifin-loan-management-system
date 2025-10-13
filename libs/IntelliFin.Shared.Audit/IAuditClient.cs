using System.Diagnostics;
using System.Linq;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntelliFin.Shared.Audit;

public interface IAuditClient
{
    Task LogEventAsync(AuditEventPayload payload, CancellationToken cancellationToken = default);
    Task LogEventsBatchAsync(IEnumerable<AuditEventPayload> payloads, CancellationToken cancellationToken = default);
}

public sealed class AuditClient : IAuditClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AuditClient> _logger;

    public AuditClient(HttpClient httpClient, IOptions<AuditClientOptions> options, ILogger<AuditClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        if (options.Value.BaseAddress is not null)
        {
            _httpClient.BaseAddress = options.Value.BaseAddress;
        }

        _httpClient.Timeout = options.Value.HttpTimeout;
    }

    public async Task LogEventAsync(AuditEventPayload payload, CancellationToken cancellationToken = default)
    {
        try
        {
            EnsureCorrelationId(payload);
            var response = await _httpClient.PostAsJsonAsync("/api/admin/audit/events", payload, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to post audit event {Action} for {EntityType}:{EntityId}", payload.Action, payload.EntityType, payload.EntityId);
            throw;
        }
    }

    public async Task LogEventsBatchAsync(IEnumerable<AuditEventPayload> payloads, CancellationToken cancellationToken = default)
    {
        try
        {
            var materialized = payloads.Select(payload =>
            {
                EnsureCorrelationId(payload);
                return payload;
            }).ToList();

            var response = await _httpClient.PostAsJsonAsync("/api/admin/audit/events/batch", new AuditBatchPayload
            {
                Events = materialized
            }, cancellationToken);

            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to post batch audit events");
            throw;
        }
    }

    private static void EnsureCorrelationId(AuditEventPayload payload)
    {
        if (!string.IsNullOrWhiteSpace(payload.CorrelationId))
        {
            return;
        }

        var traceId = Activity.Current?.TraceId.ToString();
        if (!string.IsNullOrWhiteSpace(traceId))
        {
            payload.CorrelationId = traceId;
        }
    }
}

public sealed class AuditEventPayload
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Actor { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? CorrelationId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public object? EventData { get; set; }
}

public sealed class AuditBatchPayload
{
    public List<AuditEventPayload> Events { get; set; } = new();
}
