using IntelliFin.CreditAssessmentService.Models.Responses;
using IntelliFin.Shared.DomainModels.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace IntelliFin.CreditAssessmentService.Controllers;

/// <summary>
/// Manual override controller for credit officer interventions.
/// Story 1.13: Manual Override Workflow
/// </summary>
[ApiController]
[Route("api/v1/credit-assessment")]
[Authorize]
public class ManualOverrideController : ControllerBase
{
    private readonly LmsDbContext _dbContext;
    private readonly ILogger<ManualOverrideController> _logger;

    public ManualOverrideController(LmsDbContext dbContext, ILogger<ManualOverrideController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Applies a manual override to an automated credit decision.
    /// </summary>
    /// <param name="assessmentId">Assessment to override</param>
    /// <param name="request">Override details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated assessment</returns>
    [HttpPost("{assessmentId:guid}/manual-override")]
    [ProducesResponseType(typeof(AssessmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AssessmentResponse>> ApplyManualOverride(
        Guid assessmentId,
        [FromBody] ManualOverrideRequest request,
        CancellationToken cancellationToken)
    {
        // TODO: Check for credit:override permission
        var userId = GetUserIdFromClaims();
        if (userId == null)
        {
            return Forbid();
        }

        var assessment = await _dbContext.CreditAssessments
            .FirstOrDefaultAsync(a => a.Id == assessmentId, cancellationToken);

        if (assessment == null)
        {
            return NotFound(new ErrorResponse
            {
                Type = "NotFound",
                Title = "Assessment Not Found",
                Status = 404,
                Detail = $"Assessment {assessmentId} not found"
            });
        }

        if (assessment.ManualOverrideByUserId != null)
        {
            return BadRequest(new ErrorResponse
            {
                Type = "ValidationError",
                Title = "Assessment Already Overridden",
                Status = 400,
                Detail = "This assessment has already been manually overridden"
            });
        }

        // Apply override
        assessment.ManualOverrideByUserId = userId;
        assessment.ManualOverrideReason = request.Reason;
        assessment.ManualOverrideAt = DateTime.UtcNow;
        assessment.DecisionCategory = request.Decision;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Manual override applied to assessment {AssessmentId} by user {UserId}, new decision: {Decision}",
            assessmentId, userId, request.Decision);

        return Ok(new AssessmentResponse
        {
            AssessmentId = assessment.Id,
            Decision = assessment.DecisionCategory ?? "Unknown",
            RiskGrade = assessment.RiskGrade,
            IsValid = assessment.IsValid
        });
    }

    private Guid? GetUserIdFromClaims()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        if (claim != null && Guid.TryParse(claim.Value, out var userId))
        {
            return userId;
        }
        return null;
    }
}

public class ManualOverrideRequest
{
    [Required]
    public string Decision { get; set; } = string.Empty;

    [Required]
    [MinLength(20, ErrorMessage = "Override reason must be at least 20 characters")]
    public string Reason { get; set; } = string.Empty;
}
