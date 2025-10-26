using System.Net.Http.Json;
using System.Text.Json;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace IntelliFin.CreditAssessmentService.Services.Integration;

public class AdminServiceClient : IAdminServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AdminServiceClient> _logger;
    private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
    private readonly AsyncCircuitBreakerPolicy<HttpResponseMessage> _circuitBreakerPolicy;

    public AdminServiceClient(HttpClient httpClient, ILogger<AdminServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        // Retry policy: 3 attempts with exponential backoff
        _retryPolicy = Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .OrResult(r => !r.IsSuccessStatusCode)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning("Audit logging retry {RetryCount} after {Delay}ms due to {Reason}",
                        retryCount, timespan.TotalMilliseconds, outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString());
                });

        // Circuit breaker: Break after 5 consecutive failures, stay open for 30 seconds
        _circuitBreakerPolicy = Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .OrResult(r => !r.IsSuccessStatusCode)
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (outcome, duration) =>
                {
                    _logger.LogError("Audit service circuit breaker opened for {Duration}s due to {Reason}",
                        duration.TotalSeconds, outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString());
                },
                onReset: () =>
                {
                    _logger.LogInformation("Audit service circuit breaker reset");
                });
    }

    public async Task LogAuditEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Logging audit event {EventType} for entity {EntityId}", 
                auditEvent.EventType, auditEvent.EntityId);

            // Map to AdminService audit event structure
            var adminAuditEvent = new
            {
                eventId = Guid.NewGuid(),
                timestamp = auditEvent.Timestamp,
                actor = auditEvent.UserId?.ToString() ?? "system",
                action = auditEvent.Action,
                entityType = auditEvent.EntityType,
                entityId = auditEvent.EntityId.ToString(),
                correlationId = auditEvent.CorrelationId,
                eventData = JsonSerializer.Serialize(auditEvent.Details),
                ipAddress = (string?)null,
                userAgent = (string?)null
            };

            // Execute with retry and circuit breaker
            var policy = Policy.WrapAsync(_retryPolicy, _circuitBreakerPolicy);
            
            var response = await policy.ExecuteAsync(async () =>
            {
                return await _httpClient.PostAsJsonAsync("/api/audit/events", adminAuditEvent, cancellationToken);
            });

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Audit event {EventType} logged successfully for entity {EntityId}",
                    auditEvent.EventType, auditEvent.EntityId);
            }
            else
            {
                _logger.LogWarning("Audit event logging returned {StatusCode} for {EventType}",
                    response.StatusCode, auditEvent.EventType);
            }
        }
        catch (BrokenCircuitException)
        {
            _logger.LogWarning("Audit service circuit breaker is open, event {EventType} not logged",
                auditEvent.EventType);
            // Don't throw - audit logging should not block assessment
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging audit event {EventType}", auditEvent.EventType);
            // Don't throw - audit logging should not block assessment
        }
    }
}
