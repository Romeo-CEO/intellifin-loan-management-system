using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntelliFin.CreditAssessmentService.EventHandlers;

/// <summary>
/// Test controller for publishing KYC events to verify MassTransit integration.
/// THIS IS FOR TESTING ONLY - Remove in production.
/// </summary>
[ApiController]
[Route("api/v1/test/events")]
[Authorize]
public class EventTestController : ControllerBase
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<EventTestController> _logger;

    public EventTestController(IPublishEndpoint publishEndpoint, ILogger<EventTestController> logger)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    /// <summary>
    /// Test endpoint to publish a KycExpiredEvent.
    /// </summary>
    [HttpPost("kyc-expired")]
    public async Task<IActionResult> PublishKycExpired([FromQuery] Guid clientId)
    {
        _logger.LogInformation("Publishing test KycExpiredEvent for client {ClientId}", clientId);

        var @event = new KycExpiredEvent(clientId, DateTime.UtcNow);
        await _publishEndpoint.Publish(@event);

        return Ok(new { message = "KycExpiredEvent published", clientId, timestamp = DateTime.UtcNow });
    }

    /// <summary>
    /// Test endpoint to publish a KycRevokedEvent.
    /// </summary>
    [HttpPost("kyc-revoked")]
    public async Task<IActionResult> PublishKycRevoked([FromQuery] Guid clientId, [FromQuery] string reason = "Test revocation")
    {
        _logger.LogInformation("Publishing test KycRevokedEvent for client {ClientId}", clientId);

        var @event = new KycRevokedEvent(clientId, reason, DateTime.UtcNow);
        await _publishEndpoint.Publish(@event);

        return Ok(new { message = "KycRevokedEvent published", clientId, reason, timestamp = DateTime.UtcNow });
    }

    /// <summary>
    /// Test endpoint to publish a KycUpdatedEvent.
    /// </summary>
    [HttpPost("kyc-updated")]
    public async Task<IActionResult> PublishKycUpdated([FromQuery] Guid clientId, [FromQuery] string updateType = "Renewal")
    {
        _logger.LogInformation("Publishing test KycUpdatedEvent for client {ClientId}", clientId);

        var @event = new KycUpdatedEvent(clientId, updateType, DateTime.UtcNow);
        await _publishEndpoint.Publish(@event);

        return Ok(new { message = "KycUpdatedEvent published", clientId, updateType, timestamp = DateTime.UtcNow });
    }
}
