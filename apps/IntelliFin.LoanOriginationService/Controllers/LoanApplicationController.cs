using IntelliFin.LoanOriginationService.Models;
using IntelliFin.LoanOriginationService.Services;
using IntelliFin.LoanOriginationService.Exceptions;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using IntelliFin.Shared.DomainModels.Data;
using Microsoft.EntityFrameworkCore;
using Minio;
using Minio.DataModel.Args;

namespace IntelliFin.LoanOriginationService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LoanApplicationController : ControllerBase
{
    private readonly ILogger<LoanApplicationController> _logger;
    private readonly ILoanApplicationService _loanApplicationService;
    private readonly ICreditAssessmentService _creditAssessmentService;
    private readonly ILoanProductService _productService;
    private readonly LmsDbContext _dbContext;
    private readonly IMinioClient? _minioClient;

    public LoanApplicationController(
        ILogger<LoanApplicationController> logger,
        ILoanApplicationService loanApplicationService,
        ICreditAssessmentService creditAssessmentService,
        ILoanProductService productService,
        LmsDbContext dbContext,
        IMinioClient? minioClient = null)
    {
        _logger = logger;
        _loanApplicationService = loanApplicationService;
        _creditAssessmentService = creditAssessmentService;
        _productService = productService;
        _dbContext = dbContext;
        _minioClient = minioClient;
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
        catch (KycNotVerifiedException ex)
        {
            return BadRequest(new
            {
                ErrorCode = "KYC_NOT_VERIFIED",
                Message = ex.Message,
                ClientId = ex.ClientId,
                KycStatus = ex.KycStatus,
                ActionRequired = "Complete KYC verification before applying for loans",
                KycCompletionLink = $"/clients/{ex.ClientId}/kyc"
            });
        }
        catch (KycExpiredException ex)
        {
            return BadRequest(new
            {
                ErrorCode = "KYC_EXPIRED",
                Message = ex.Message,
                ClientId = ex.ClientId,
                KycApprovedAt = ex.KycApprovedAt,
                ExpiryDate = ex.ExpiryDate,
                ActionRequired = "Renew KYC verification (expired)",
                KycRenewalLink = $"/clients/{ex.ClientId}/kyc/renew"
            });
        }
        catch (ClientManagementServiceException ex)
        {
            _logger.LogError(ex, "Client Management Service unavailable during loan application creation");
            return StatusCode(503, new
            {
                ErrorCode = "CLIENT_MANAGEMENT_SERVICE_UNAVAILABLE",
                Message = "Unable to verify client KYC status. The service is temporarily unavailable. Please try again later.",
                Details = ex.Message
            });
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
            // Extract approver user ID from authentication claims
            var approverUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                ?? User.FindFirst("sub")?.Value 
                ?? request.ApprovedBy; // Fallback to request body for backward compatibility
            
            if (string.IsNullOrEmpty(approverUserId))
            {
                return Unauthorized(new { error = "User identity not found. Please ensure you are authenticated." });
            }
            
            var application = await _loanApplicationService.ApproveApplicationAsync(applicationId, approverUserId, cancellationToken);
            return Ok(application);
        }
        catch (DualControlViolationException ex)
        {
            _logger.LogWarning(ex, "Dual control violation for loan {ApplicationId}", applicationId);
            return StatusCode(403, new
            {
                ErrorCode = "DUAL_CONTROL_VIOLATION",
                Message = ex.Message,
                ApplicationId = ex.ApplicationId,
                ApproverUserId = ex.ApproverUserId,
                ViolationType = ex.ViolationType,
                ActionRequired = "A different user must approve this loan application to maintain segregation of duties. Contact your supervisor or another authorized approver."
            });
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
    
    /// <summary>
    /// Download loan agreement PDF
    /// </summary>
    [HttpGet("{id:guid}/agreement")]
    public async Task<IActionResult> DownloadAgreement(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if MinIO client is configured
            if (_minioClient == null)
            {
                _logger.LogWarning("MinIO client not configured, cannot download agreement");
                return StatusCode(503, new { error = "Document storage service not available" });
            }
            
            // Fetch loan application
            var application = await _dbContext.LoanApplications
                .FirstOrDefaultAsync(la => la.Id == id, cancellationToken);
            
            if (application == null)
            {
                return NotFound(new { error = "Loan application not found" });
            }
            
            // Check if agreement has been generated
            if (string.IsNullOrEmpty(application.AgreementMinioPath))
            {
                return NotFound(new { error = "Agreement not yet generated for this loan application" });
            }
            
            _logger.LogInformation(
                "Downloading agreement for loan {LoanNumber} from MinIO path: {MinioPath}",
                application.LoanNumber, application.AgreementMinioPath);
            
            // Download PDF from MinIO
            var stream = new MemoryStream();
            
            await _minioClient.GetObjectAsync(new GetObjectArgs()
                .WithBucket("intellifin-documents")
                .WithObject(application.AgreementMinioPath)
                .WithCallbackStream(async (s, ct) => await s.CopyToAsync(stream, ct)),
                cancellationToken);
            
            stream.Position = 0;
            
            // Return PDF file
            var fileName = $"{application.LoanNumber}_Agreement.pdf";
            return File(stream, "application/pdf", fileName);
        }
        catch (Minio.Exceptions.ObjectNotFoundException)
        {
            _logger.LogWarning("Agreement file not found in MinIO for loan application {ApplicationId}", id);
            return NotFound(new { error = "Agreement file not found in storage" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading agreement for loan application {ApplicationId}", id);
            return StatusCode(500, new { error = "An error occurred while downloading the agreement" });
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