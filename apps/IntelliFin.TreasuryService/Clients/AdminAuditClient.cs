using IntelliFin.Shared.Audit;
using IntelliFin.TreasuryService.Data;
using Microsoft.Extensions.Options;

namespace IntelliFin.TreasuryService.Clients;

public interface IAdminAuditClient
{
    Task ForwardAuditEventAsync(AuditEventPayload auditEvent, CancellationToken cancellationToken);
    Task ForwardAuditEventsBatchAsync(IEnumerable<AuditEventPayload> auditEvents, CancellationToken cancellationToken);
}

public sealed class AdminAuditClient : IAdminAuditClient
{
    private readonly HttpClient _httpClient;
    private readonly AuditClientOptions _options;
    private readonly ILogger<AdminAuditClient> _logger;

    public AdminAuditClient(
        HttpClient httpClient,
        IOptionsMonitor<AuditClientOptions> options,
        ILogger<AdminAuditClient> logger)
    {
        _httpClient = httpClient;
        _options = options.CurrentValue;
        _logger = logger;
    }

    public async Task ForwardAuditEventAsync(AuditEventPayload auditEvent, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/admin/audit/events", auditEvent, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to forward audit event to AdminService. Status: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error forwarding audit event to AdminService");
        }
    }

    public async Task ForwardAuditEventsBatchAsync(IEnumerable<AuditEventPayload> auditEvents, CancellationToken cancellationToken)
    {
        try
        {
            var batchRequest = new AuditBatchPayload
            {
                Events = auditEvents.ToList()
            };

            var response = await _httpClient.PostAsJsonAsync("/api/admin/audit/events/batch", batchRequest, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to forward audit events batch to AdminService. Status: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error forwarding audit events batch to AdminService");
        }
    }
}
