using IntelliFin.ClientManagement.Controllers.DTOs;
using IntelliFin.ClientManagement.Domain.Enums;
using IntelliFin.ClientManagement.Domain.Exceptions;
using IntelliFin.ClientManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace IntelliFin.ClientManagement.Controllers;

/// <summary>
/// API controller for KYC (Know Your Customer) workflow management
/// </summary>
[ApiController]
[Route("api/clients/{clientId:guid}/kyc")]
[Authorize]
public class KycController : ControllerBase
{
    private readonly IKycWorkflowService _kycWorkflowService;
    private readonly ILogger<KycController> _logger;

    public KycController(
        IKycWorkflowService kycWorkflowService,
        ILogger<KycController> logger)
    {
        _kycWorkflowService = kycWorkflowService;
        _logger = logger;
    }

    /// <summary>
    /// Initiates KYC process for a client
    /// </summary>
    /// <param name="clientId">Client unique identifier</param>
    /// <param name="request">Initiation request (optional notes)</param>
    /// <returns>Created KYC status</returns>
    [HttpPost("initiate")]
    [ProducesResponseType(typeof(KycStatusResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> InitiateKyc(
        Guid clientId,
        [FromBody] InitiateKycRequest? request)
    {
        var userId = GetUserId();

        _logger.LogInformation(
            "Initiating KYC for client {ClientId} by user {UserId}",
            clientId, userId);

        var result = await _kycWorkflowService.InitiateKycAsync(
            clientId, userId, request?.Notes);

        if (result.IsFailure)
        {
            if (result.Error.Contains("not found"))
            {
                return NotFound(new { error = result.Error });
            }

            if (result.Error.Contains("already exists"))
            {
                return Conflict(new { error = result.Error });
            }

            return BadRequest(new { error = result.Error });
        }

        return CreatedAtAction(
            nameof(GetKycStatus),
            new { clientId },
            result.Value);
    }

    /// <summary>
    /// Gets current KYC status for a client
    /// </summary>
    /// <param name="clientId">Client unique identifier</param>
    /// <returns>KYC status information</returns>
    [HttpGet("status")]
    [ProducesResponseType(typeof(KycStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetKycStatus(Guid clientId)
    {
        _logger.LogDebug("Getting KYC status for client {ClientId}", clientId);

        var result = await _kycWorkflowService.GetKycStatusAsync(clientId);

        if (result.IsFailure)
        {
            return NotFound(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Updates KYC state for a client
    /// Validates state transitions and business rules
    /// </summary>
    /// <param name="clientId">Client unique identifier</param>
    /// <param name="request">State update request</param>
    /// <returns>Updated KYC status</returns>
    [HttpPut("state")]
    [ProducesResponseType(typeof(KycStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateKycState(
        Guid clientId,
        [FromBody] UpdateKycStateRequest request)
    {
        var userId = GetUserId();

        _logger.LogInformation(
            "Updating KYC state for client {ClientId} to {NewState} by user {UserId}",
            clientId, request.NewState, userId);

        try
        {
            // Parse new state from string
            if (!Enum.TryParse<KycState>(request.NewState, out var newState))
            {
                return BadRequest(new
                {
                    error = "Invalid state",
                    message = $"'{request.NewState}' is not a valid KYC state. " +
                             "Valid values: Pending, InProgress, Completed, EDD_Required, Rejected"
                });
            }

            var result = await _kycWorkflowService.UpdateKycStateAsync(
                clientId, newState, request, userId);

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
        catch (InvalidKycStateTransitionException ex)
        {
            _logger.LogWarning(ex,
                "Invalid KYC state transition for client {ClientId}",
                clientId);

            return BadRequest(new
            {
                error = "Invalid state transition",
                message = ex.Message,
                details = new
                {
                    fromState = ex.FromState.ToString(),
                    toState = ex.ToState.ToString(),
                    reason = ex.Reason
                }
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex,
                "Business rule violation for client {ClientId}",
                clientId);

            return UnprocessableEntity(new
            {
                error = "Business rule violation",
                message = ex.Message
            });
        }
    }

    private string GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? "system";
    }
}
