using IntelliFin.CreditAssessmentService.Models.Requests;
using IntelliFin.CreditAssessmentService.Models.Responses;
using IntelliFin.CreditAssessmentService.Services.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace IntelliFin.CreditAssessmentService.Controllers;

/// <summary>
/// Credit assessment API controller.
/// </summary>
[ApiController]
[Route("api/v1/credit-assessment")]
[Authorize]
[Produces("application/json")]
public class CreditAssessmentController : ControllerBase
{
    private readonly ICreditAssessmentService _assessmentService;
    private readonly ILogger<CreditAssessmentController> _logger;

    public CreditAssessmentController(
        ICreditAssessmentService assessmentService,
        ILogger<CreditAssessmentController> logger)
    {
        _assessmentService = assessmentService;
        _logger = logger;
    }

    /// <summary>
    /// Performs a credit assessment for a loan application.
    /// </summary>
    /// <param name="request">Assessment request details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Complete assessment result including decision, risk grade, and explanation</returns>
    /// <response code="200">Assessment completed successfully</response>
    /// <response code="400">Invalid request parameters</response>
    /// <response code="401">Unauthorized - missing or invalid token</response>
    /// <response code="403">Forbidden - insufficient permissions</response>
    /// <response code="500">Internal server error</response>
    [HttpPost("assess")]
    [ProducesResponseType(typeof(AssessmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AssessmentResponse>> PerformAssessment(
        [FromBody] AssessmentRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromClaims();
        var correlationId = HttpContext.TraceIdentifier;

        _logger.LogInformation(
            "Credit assessment requested for loan {LoanApplicationId} by user {UserId}, correlation {CorrelationId}",
            request.LoanApplicationId, userId, correlationId);

        try
        {
            var result = await _assessmentService.PerformAssessmentAsync(
                request, userId, cancellationToken);

            _logger.LogInformation(
                "Assessment {AssessmentId} completed with decision {Decision}, correlation {CorrelationId}",
                result.AssessmentId, result.Decision, correlationId);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error performing assessment for loan {LoanApplicationId}, correlation {CorrelationId}",
                request.LoanApplicationId, correlationId);

            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
            {
                Type = "AssessmentError",
                Title = "Credit Assessment Failed",
                Status = StatusCodes.Status500InternalServerError,
                Detail = "An error occurred while performing the credit assessment",
                CorrelationId = correlationId
            });
        }
    }

    /// <summary>
    /// Retrieves an existing credit assessment by ID.
    /// </summary>
    /// <param name="assessmentId">Unique assessment identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Assessment details</returns>
    /// <response code="200">Assessment found and returned</response>
    /// <response code="404">Assessment not found</response>
    /// <response code="401">Unauthorized</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("{assessmentId:guid}")]
    [ProducesResponseType(typeof(AssessmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AssessmentResponse>> GetAssessment(
        Guid assessmentId,
        CancellationToken cancellationToken)
    {
        var correlationId = HttpContext.TraceIdentifier;

        _logger.LogInformation(
            "Retrieving assessment {AssessmentId}, correlation {CorrelationId}",
            assessmentId, correlationId);

        try
        {
            var result = await _assessmentService.GetAssessmentByIdAsync(
                assessmentId, cancellationToken);

            if (result == null)
            {
                _logger.LogWarning(
                    "Assessment {AssessmentId} not found, correlation {CorrelationId}",
                    assessmentId, correlationId);

                return NotFound(new ErrorResponse
                {
                    Type = "NotFound",
                    Title = "Assessment Not Found",
                    Status = StatusCodes.Status404NotFound,
                    Detail = $"Assessment with ID {assessmentId} was not found",
                    CorrelationId = correlationId
                });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error retrieving assessment {AssessmentId}, correlation {CorrelationId}",
                assessmentId, correlationId);

            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
            {
                Type = "ServerError",
                Title = "Error Retrieving Assessment",
                Status = StatusCodes.Status500InternalServerError,
                Detail = "An error occurred while retrieving the assessment",
                CorrelationId = correlationId
            });
        }
    }

    /// <summary>
    /// Retrieves the latest credit assessment for a specific client.
    /// </summary>
    /// <param name="clientId">Client unique identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Latest assessment for the client</returns>
    /// <response code="200">Assessment found and returned</response>
    /// <response code="404">No assessments found for client</response>
    /// <response code="401">Unauthorized</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("client/{clientId:guid}/latest")]
    [ProducesResponseType(typeof(AssessmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AssessmentResponse>> GetLatestAssessmentForClient(
        Guid clientId,
        CancellationToken cancellationToken)
    {
        var correlationId = HttpContext.TraceIdentifier;

        _logger.LogInformation(
            "Retrieving latest assessment for client {ClientId}, correlation {CorrelationId}",
            clientId, correlationId);

        try
        {
            var result = await _assessmentService.GetLatestAssessmentForClientAsync(
                clientId, cancellationToken);

            if (result == null)
            {
                _logger.LogInformation(
                    "No assessments found for client {ClientId}, correlation {CorrelationId}",
                    clientId, correlationId);

                return NotFound(new ErrorResponse
                {
                    Type = "NotFound",
                    Title = "No Assessments Found",
                    Status = StatusCodes.Status404NotFound,
                    Detail = $"No credit assessments found for client {clientId}",
                    CorrelationId = correlationId
                });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error retrieving latest assessment for client {ClientId}, correlation {CorrelationId}",
                clientId, correlationId);

            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
            {
                Type = "ServerError",
                Title = "Error Retrieving Assessment",
                Status = StatusCodes.Status500InternalServerError,
                Detail = "An error occurred while retrieving the client's assessment",
                CorrelationId = correlationId
            });
        }
    }

    /// <summary>
    /// Health check endpoint for the controller.
    /// </summary>
    /// <returns>Health status</returns>
    [HttpGet("health")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult HealthCheck()
    {
        return Ok(new { status = "healthy", controller = "CreditAssessmentController" });
    }

    /// <summary>
    /// Extracts user ID from JWT claims.
    /// </summary>
    private Guid? GetUserIdFromClaims()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) 
                         ?? User.FindFirst("sub")
                         ?? User.FindFirst("user_id");

        if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return userId;
        }

        return null;
    }
}
