using IntelliFin.LoanOriginationService.Models;
using IntelliFin.LoanOriginationService.Services;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace IntelliFin.LoanOriginationService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LoanApplicationController : ControllerBase
{
    private readonly ILogger<LoanApplicationController> _logger;
    private readonly ILoanApplicationService _loanApplicationService;
    private readonly ICreditAssessmentService _creditAssessmentService;
    private readonly ILoanProductService _productService;

    public LoanApplicationController(
        ILogger<LoanApplicationController> logger,
        ILoanApplicationService loanApplicationService,
        ICreditAssessmentService creditAssessmentService,
        ILoanProductService productService)
    {
        _logger = logger;
        _loanApplicationService = loanApplicationService;
        _creditAssessmentService = creditAssessmentService;
        _productService = productService;
    }

    /// <summary>
    /// Create a new loan application
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<LoanApplicationResponse>> CreateApplication(
        [FromBody] CreateLoanApplicationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var application = await _loanApplicationService.CreateApplicationAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetApplication), new { applicationId = application.Id }, application);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating loan application");
            return StatusCode(500, new { error = "An error occurred while creating the application" });
        }
    }

    /// <summary>
    /// Get loan application by ID
    /// </summary>
    [HttpGet("{applicationId:guid}")]
    public async Task<ActionResult<LoanApplicationResponse>> GetApplication(
        Guid applicationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var application = await _loanApplicationService.GetApplicationAsync(applicationId, cancellationToken);
            if (application == null)
            {
                return NotFound(new { error = "Application not found" });
            }

            return Ok(application);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving loan application {ApplicationId}", applicationId);
            return StatusCode(500, new { error = "An error occurred while retrieving the application" });
        }
    }

    /// <summary>
    /// Get loan applications for a client
    /// </summary>
    [HttpGet("client/{clientId:guid}")]
    public async Task<ActionResult<IEnumerable<LoanApplicationResponse>>> GetClientApplications(
        Guid clientId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var applications = await _loanApplicationService.GetApplicationsByClientAsync(clientId, cancellationToken);
            return Ok(applications);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving applications for client {ClientId}", clientId);
            return StatusCode(500, new { error = "An error occurred while retrieving applications" });
        }
    }

    /// <summary>
    /// Update loan application data
    /// </summary>
    [HttpPatch("{applicationId:guid}")]
    public async Task<ActionResult<LoanApplicationResponse>> UpdateApplication(
        Guid applicationId,
        [FromBody] Dictionary<string, object> updates,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var application = await _loanApplicationService.UpdateApplicationAsync(applicationId, updates, cancellationToken);
            return Ok(application);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "Application not found" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating loan application {ApplicationId}", applicationId);
            return StatusCode(500, new { error = "An error occurred while updating the application" });
        }
    }

    /// <summary>
    /// Submit loan application for processing
    /// </summary>
    [HttpPost("{applicationId:guid}/submit")]
    public async Task<ActionResult> SubmitApplication(
        Guid applicationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var success = await _loanApplicationService.SubmitApplicationAsync(applicationId, cancellationToken);
            if (success)
            {
                return Ok(new { message = "Application submitted successfully" });
            }
            return BadRequest(new { error = "Failed to submit application" });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "Application not found" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting loan application {ApplicationId}", applicationId);
            return StatusCode(500, new { error = "An error occurred while submitting the application" });
        }
    }

    /// <summary>
    /// Withdraw loan application
    /// </summary>
    [HttpPost("{applicationId:guid}/withdraw")]
    public async Task<ActionResult> WithdrawApplication(
        Guid applicationId,
        [FromBody] WithdrawApplicationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var success = await _loanApplicationService.WithdrawApplicationAsync(applicationId, request.Reason, cancellationToken);
            if (success)
            {
                return Ok(new { message = "Application withdrawn successfully" });
            }
            return BadRequest(new { error = "Failed to withdraw application" });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "Application not found" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error withdrawing loan application {ApplicationId}", applicationId);
            return StatusCode(500, new { error = "An error occurred while withdrawing the application" });
        }
    }

    /// <summary>
    /// Approve loan application
    /// </summary>
    [HttpPost("{applicationId:guid}/approve")]
    public async Task<ActionResult<LoanApplicationResponse>> ApproveApplication(
        Guid applicationId,
        [FromBody] ApproveApplicationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var application = await _loanApplicationService.ApproveApplicationAsync(applicationId, request.ApprovedBy, cancellationToken);
            return Ok(application);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "Application not found" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving loan application {ApplicationId}", applicationId);
            return StatusCode(500, new { error = "An error occurred while approving the application" });
        }
    }

    /// <summary>
    /// Reject loan application
    /// </summary>
    [HttpPost("{applicationId:guid}/reject")]
    public async Task<ActionResult<LoanApplicationResponse>> RejectApplication(
        Guid applicationId,
        [FromBody] RejectApplicationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var application = await _loanApplicationService.RejectApplicationAsync(
                applicationId, request.RejectedBy, request.Reason, cancellationToken);
            return Ok(application);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "Application not found" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting loan application {ApplicationId}", applicationId);
            return StatusCode(500, new { error = "An error occurred while rejecting the application" });
        }
    }

    /// <summary>
    /// Validate loan application
    /// </summary>
    [HttpPost("{applicationId:guid}/validate")]
    public async Task<ActionResult<RuleEngineResult>> ValidateApplication(
        Guid applicationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var validationResult = await _loanApplicationService.ValidateApplicationAsync(applicationId, cancellationToken);
            return Ok(validationResult);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "Application not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating loan application {ApplicationId}", applicationId);
            return StatusCode(500, new { error = "An error occurred while validating the application" });
        }
    }
}

// Request DTOs
public class WithdrawApplicationRequest
{
    [Required]
    public string Reason { get; set; } = string.Empty;
}

public class ApproveApplicationRequest
{
    [Required]
    public string ApprovedBy { get; set; } = string.Empty;
}

public class RejectApplicationRequest
{
    [Required]
    public string RejectedBy { get; set; } = string.Empty;
    
    [Required]
    public string Reason { get; set; } = string.Empty;
}