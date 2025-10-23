using IntelliFin.ClientManagement.Controllers.DTOs;
using IntelliFin.ClientManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace IntelliFin.ClientManagement.Controllers;

/// <summary>
/// API controller for managing client communication consent preferences
/// </summary>
[ApiController]
[Route("api/clients/{clientId:guid}/consents")]
[Authorize]
public class ClientConsentController : ControllerBase
{
    private readonly IConsentManagementService _consentService;
    private readonly ILogger<ClientConsentController> _logger;

    public ClientConsentController(
        IConsentManagementService consentService,
        ILogger<ClientConsentController> logger)
    {
        _consentService = consentService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all communication consent preferences for a client
    /// </summary>
    /// <param name="clientId">Client unique identifier</param>
    /// <returns>List of consent preferences</returns>
    [HttpGet]
    [ProducesResponseType(typeof(List<ConsentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetConsents(Guid clientId)
    {
        var result = await _consentService.GetAllConsentsAsync(clientId);

        if (result.IsFailure)
        {
            if (result.Error.Contains("not found"))
            {
                return NotFound(new { error = result.Error });
            }

            return StatusCode(500, new { error = result.Error });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Gets consent preferences for a specific consent type
    /// </summary>
    /// <param name="clientId">Client unique identifier</param>
    /// <param name="consentType">Consent type (Marketing, Operational, Regulatory)</param>
    /// <returns>Consent preferences or 404 if not found</returns>
    [HttpGet("{consentType}")]
    [ProducesResponseType(typeof(ConsentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetConsent(Guid clientId, string consentType)
    {
        var result = await _consentService.GetConsentAsync(clientId, consentType);

        if (result.IsFailure)
        {
            return StatusCode(500, new { error = result.Error });
        }

        if (result.Value == null)
        {
            return NotFound(new { error = $"Consent type '{consentType}' not found for client" });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Updates or creates communication consent preferences
    /// </summary>
    /// <param name="clientId">Client unique identifier</param>
    /// <param name="request">Updated consent preferences</param>
    /// <returns>Updated consent preferences</returns>
    [HttpPut]
    [ProducesResponseType(typeof(ConsentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateConsent(
        Guid clientId,
        [FromBody] UpdateConsentRequest request)
    {
        var userId = GetUserId();
        var correlationId = GetCorrelationId();

        _logger.LogInformation(
            "Consent update request: ClientId={ClientId}, ConsentType={ConsentType}, User={UserId}",
            clientId, request.ConsentType, userId);

        var result = await _consentService.UpdateConsentAsync(
            clientId, request, userId, correlationId);

        if (result.IsFailure)
        {
            if (result.Error.Contains("not found"))
            {
                return NotFound(new { error = result.Error });
            }

            if (result.Error.Contains("Regulatory consent cannot be disabled") ||
                result.Error.Contains("Invalid"))
            {
                return BadRequest(new { error = result.Error });
            }

            return StatusCode(500, new { error = result.Error });
        }

        return Ok(result.Value);
    }

    private string GetUserId()
    {
        // Extract user ID from JWT claims
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? "system";
    }

    private string? GetCorrelationId()
    {
        // Try to get correlation ID from header
        if (HttpContext.Request.Headers.TryGetValue("X-Correlation-Id", out var correlationId))
        {
            return correlationId.FirstOrDefault();
        }

        return HttpContext.TraceIdentifier;
    }
}
