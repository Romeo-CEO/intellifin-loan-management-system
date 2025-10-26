using IntelliFin.ClientManagement.Controllers.DTOs;
using IntelliFin.ClientManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntelliFin.ClientManagement.Controllers;

/// <summary>
/// API controller for client risk profiles
/// Provides risk assessment and historical trend data
/// </summary>
[ApiController]
[Route("api/clients/{clientId:guid}/risk")]
[Authorize]
public class RiskProfileController : ControllerBase
{
    private readonly IRiskScoringService _riskScoringService;
    private readonly ILogger<RiskProfileController> _logger;

    public RiskProfileController(
        IRiskScoringService riskScoringService,
        ILogger<RiskProfileController> logger)
    {
        _riskScoringService = riskScoringService;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current risk profile for a client
    /// </summary>
    /// <param name="clientId">Client unique identifier</param>
    /// <returns>Current risk profile</returns>
    [HttpGet("profile")]
    [ProducesResponseType(typeof(RiskProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RiskProfileResponse>> GetCurrentRiskProfile(Guid clientId)
    {
        _logger.LogInformation("Getting current risk profile for client {ClientId}", clientId);

        var riskProfile = await _riskScoringService.GetCurrentRiskProfileAsync(clientId);

        if (riskProfile == null)
        {
            _logger.LogWarning("No risk profile found for client {ClientId}", clientId);
            return NotFound(new { error = "No risk profile found for this client" });
        }

        var response = new RiskProfileResponse
        {
            Id = riskProfile.Id,
            ClientId = riskProfile.ClientId,
            RiskRating = riskProfile.RiskRating,
            RiskScore = riskProfile.RiskScore,
            ComputedAt = riskProfile.ComputedAt,
            ComputedBy = riskProfile.ComputedBy,
            RiskRulesVersion = riskProfile.RiskRulesVersion,
            IsCurrent = riskProfile.IsCurrent
        };

        return Ok(response);
    }

    /// <summary>
    /// Gets risk assessment history for a client
    /// </summary>
    /// <param name="clientId">Client unique identifier</param>
    /// <returns>Historical risk profiles with trend analysis</returns>
    [HttpGet("history")]
    [ProducesResponseType(typeof(RiskHistoryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<RiskHistoryResponse>> GetRiskHistory(Guid clientId)
    {
        _logger.LogInformation("Getting risk history for client {ClientId}", clientId);

        var history = await _riskScoringService.GetRiskHistoryAsync(clientId);

        var profiles = history.Select(p => new RiskProfileResponse
        {
            Id = p.Id,
            ClientId = p.ClientId,
            RiskRating = p.RiskRating,
            RiskScore = p.RiskScore,
            ComputedAt = p.ComputedAt,
            ComputedBy = p.ComputedBy,
            RiskRulesVersion = p.RiskRulesVersion,
            IsCurrent = p.IsCurrent
        }).ToList();

        // Calculate trend
        RiskTrendSummary? trend = null;
        if (profiles.Count >= 2)
        {
            var current = profiles.First();
            var previous = profiles.Skip(1).First();

            var trendDirection = current.RiskScore > previous.RiskScore ? "Increasing" :
                                current.RiskScore < previous.RiskScore ? "Decreasing" : "Stable";

            trend = new RiskTrendSummary
            {
                CurrentRating = current.RiskRating,
                PreviousRating = previous.RiskRating,
                Trend = trendDirection,
                AverageScore = (int)profiles.Average(p => p.RiskScore)
            };
        }

        var response = new RiskHistoryResponse
        {
            ClientId = clientId,
            Profiles = profiles,
            TotalAssessments = profiles.Count,
            Trend = trend
        };

        return Ok(response);
    }

    /// <summary>
    /// Triggers manual risk recalculation for a client
    /// </summary>
    /// <param name="clientId">Client unique identifier</param>
    /// <param name="reason">Reason for recalculation (optional)</param>
    /// <returns>New risk profile</returns>
    [HttpPost("recompute")]
    [ProducesResponseType(typeof(RiskProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RiskProfileResponse>> RecomputeRisk(
        Guid clientId,
        [FromQuery] string? reason = null)
    {
        var userId = User.FindFirst("sub")?.Value ?? "unknown";

        _logger.LogInformation(
            "Manual risk recalculation requested for client {ClientId} by {UserId}: Reason={Reason}",
            clientId, userId, reason);

        var result = await _riskScoringService.RecomputeRiskAsync(
            clientId,
            reason ?? "ManualRecalculation",
            userId);

        if (result.IsFailure)
        {
            _logger.LogError("Risk recalculation failed for client {ClientId}: {Error}", clientId, result.Error);
            return BadRequest(new { error = result.Error });
        }

        var riskProfile = result.Value!;

        var response = new RiskProfileResponse
        {
            Id = riskProfile.Id,
            ClientId = riskProfile.ClientId,
            RiskRating = riskProfile.RiskRating,
            RiskScore = riskProfile.RiskScore,
            ComputedAt = riskProfile.ComputedAt,
            ComputedBy = riskProfile.ComputedBy,
            RiskRulesVersion = riskProfile.RiskRulesVersion,
            IsCurrent = riskProfile.IsCurrent
        };

        return Ok(response);
    }

    /// <summary>
    /// Gets the input factors used for risk scoring
    /// </summary>
    /// <param name="clientId">Client unique identifier</param>
    /// <returns>Current input factors</returns>
    [HttpGet("factors")]
    [ProducesResponseType(typeof(Domain.Models.InputFactors), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Domain.Models.InputFactors>> GetInputFactors(Guid clientId)
    {
        _logger.LogInformation("Getting risk input factors for client {ClientId}", clientId);

        var result = await _riskScoringService.BuildInputFactorsAsync(clientId);

        if (result.IsFailure)
        {
            return NotFound(new { error = result.Error });
        }

        return Ok(result.Value);
    }
}
