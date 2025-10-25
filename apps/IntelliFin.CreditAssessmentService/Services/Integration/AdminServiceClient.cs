using System.Net.Http.Json;

namespace IntelliFin.CreditAssessmentService.Services.Integration;

public class AdminServiceClient : IAdminServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AdminServiceClient> _logger;

    public AdminServiceClient(HttpClient httpClient, ILogger<AdminServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task LogAuditEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Logging audit event {EventType} for entity {EntityId}", 
                auditEvent.EventType, auditEvent.EntityId);

            // TODO: Implement actual HTTP call to AdminService
            // await _httpClient.PostAsJsonAsync("/api/v1/audit", auditEvent, cancellationToken);
            
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging audit event {EventType}", auditEvent.EventType);
            // Don't throw - audit logging should not block assessment
        }
    }
}
