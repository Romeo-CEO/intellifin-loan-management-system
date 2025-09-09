using IntelliFin.KycDocumentService.Models;
using IntelliFin.KycDocumentService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace IntelliFin.KycDocumentService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class KycDocumentController : ControllerBase
{
    private readonly IKycDocumentService _kycDocumentService;
    private readonly ILogger<KycDocumentController> _logger;

    public KycDocumentController(IKycDocumentService kycDocumentService, ILogger<KycDocumentController> logger)
    {
        _kycDocumentService = kycDocumentService;
        _logger = logger;
    }

    [HttpPost("upload")]
    [ProducesResponseType(typeof(DocumentUploadResponse), (int)HttpStatusCode.Created)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.BadRequest)]
    [Authorize(Roles = "LoanOfficer,Manager,Admin")]
    public async Task<IActionResult> UploadDocumentAsync([FromForm] DocumentUploadRequest request, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var uploadedBy = User.FindFirst("sub")?.Value ?? "system";
            var result = await _kycDocumentService.UploadDocumentAsync(request, uploadedBy, cancellationToken);

            _logger.LogInformation("Document uploaded successfully: {DocumentId} for client {ClientId}", 
                result.DocumentId, request.ClientId);

            return CreatedAtAction(nameof(GetDocumentAsync), new { id = result.DocumentId }, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading document for client {ClientId}", request.ClientId);
            return Problem(
                title: "Document Upload Error",
                detail: "An error occurred while uploading the document",
                statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(KycDocument), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> GetDocumentAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            var document = await _kycDocumentService.GetDocumentAsync(id, cancellationToken);
            
            if (document == null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Document Not Found",
                    Detail = $"Document with ID {id} was not found",
                    Status = (int)HttpStatusCode.NotFound
                });
            }

            return Ok(document);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving document {DocumentId}", id);
            return Problem(
                title: "Document Retrieval Error",
                detail: "An error occurred while retrieving the document",
                statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    [HttpGet("client/{clientId}")]
    [ProducesResponseType(typeof(IEnumerable<KycDocument>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetClientDocumentsAsync(string clientId, CancellationToken cancellationToken = default)
    {
        try
        {
            var documents = await _kycDocumentService.GetClientDocumentsAsync(clientId, cancellationToken);
            return Ok(documents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving documents for client {ClientId}", clientId);
            return Problem(
                title: "Client Documents Retrieval Error",
                detail: "An error occurred while retrieving client documents",
                statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    [HttpPost("{id}/validate")]
    [ProducesResponseType(typeof(DocumentValidationResult), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.NotFound)]
    [Authorize(Roles = "ComplianceOfficer,Manager,Admin")]
    public async Task<IActionResult> ValidateDocumentAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            var validationResult = await _kycDocumentService.ValidateDocumentAsync(id, cancellationToken);
            return Ok(validationResult);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Document Not Found",
                Detail = $"Document with ID {id} was not found",
                Status = (int)HttpStatusCode.NotFound
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating document {DocumentId}", id);
            return Problem(
                title: "Document Validation Error",
                detail: "An error occurred while validating the document",
                statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    [HttpPost("{id}/approve")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.NotFound)]
    [Authorize(Roles = "ComplianceOfficer,Manager,Admin")]
    public async Task<IActionResult> ApproveDocumentAsync(string id, 
        [FromBody] ApprovalRequest? request = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var approvedBy = User.FindFirst("sub")?.Value ?? "system";
            var success = await _kycDocumentService.ApproveDocumentAsync(id, approvedBy, 
                request?.Notes, cancellationToken);

            if (!success)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Document Not Found",
                    Detail = $"Document with ID {id} was not found",
                    Status = (int)HttpStatusCode.NotFound
                });
            }

            _logger.LogInformation("Document {DocumentId} approved by {ApprovedBy}", id, approvedBy);
            return Ok(new { message = "Document approved successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving document {DocumentId}", id);
            return Problem(
                title: "Document Approval Error",
                detail: "An error occurred while approving the document",
                statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    [HttpPost("{id}/reject")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.NotFound)]
    [Authorize(Roles = "ComplianceOfficer,Manager,Admin")]
    public async Task<IActionResult> RejectDocumentAsync(string id, 
        [FromBody] RejectionRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Rejection Reason Required",
                    Detail = "A reason must be provided when rejecting a document",
                    Status = (int)HttpStatusCode.BadRequest
                });
            }

            var rejectedBy = User.FindFirst("sub")?.Value ?? "system";
            var success = await _kycDocumentService.RejectDocumentAsync(id, rejectedBy, 
                request.Reason, cancellationToken);

            if (!success)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Document Not Found",
                    Detail = $"Document with ID {id} was not found",
                    Status = (int)HttpStatusCode.NotFound
                });
            }

            _logger.LogInformation("Document {DocumentId} rejected by {RejectedBy} with reason: {Reason}", 
                id, rejectedBy, request.Reason);
            
            return Ok(new { message = "Document rejected successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting document {DocumentId}", id);
            return Problem(
                title: "Document Rejection Error",
                detail: "An error occurred while rejecting the document",
                statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    [HttpGet("{id}/download")]
    [ProducesResponseType(typeof(string), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.NotFound)]
    [Authorize(Roles = "LoanOfficer,ComplianceOfficer,Manager,Admin")]
    public async Task<IActionResult> GetDocumentDownloadUrlAsync(string id, 
        [FromQuery] int expiryMinutes = 15, CancellationToken cancellationToken = default)
    {
        try
        {
            var expiry = TimeSpan.FromMinutes(Math.Min(expiryMinutes, 60)); // Max 1 hour
            var downloadUrl = await _kycDocumentService.GetDocumentDownloadUrlAsync(id, expiry, cancellationToken);

            return Ok(new { downloadUrl, expiresIn = expiry.TotalMinutes });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Document Not Found",
                Detail = $"Document with ID {id} was not found",
                Status = (int)HttpStatusCode.NotFound
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating download URL for document {DocumentId}", id);
            return Problem(
                title: "Download URL Error",
                detail: "An error occurred while generating the download URL",
                statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    [HttpGet("review-queue")]
    [ProducesResponseType(typeof(IEnumerable<KycDocument>), (int)HttpStatusCode.OK)]
    [Authorize(Roles = "ComplianceOfficer,Manager,Admin")]
    public async Task<IActionResult> GetDocumentsForReviewAsync(
        [FromQuery] int limit = 50, CancellationToken cancellationToken = default)
    {
        try
        {
            var documents = await _kycDocumentService.GetDocumentsForReviewAsync(
                Math.Min(limit, 100), cancellationToken);
            
            return Ok(documents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving documents for review");
            return Problem(
                title: "Review Queue Error",
                detail: "An error occurred while retrieving documents for review",
                statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    [HttpGet("statistics")]
    [ProducesResponseType(typeof(DocumentStatistics), (int)HttpStatusCode.OK)]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> GetDocumentStatisticsAsync(
        [FromQuery] DateTime? fromDate = null, 
        [FromQuery] DateTime? toDate = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var statistics = await _kycDocumentService.GetDocumentStatisticsAsync(fromDate, toDate, cancellationToken);
            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving document statistics");
            return Problem(
                title: "Statistics Error",
                detail: "An error occurred while retrieving document statistics",
                statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    [HttpGet("compliance-report")]
    [ProducesResponseType(typeof(ComplianceReport), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.BadRequest)]
    [Authorize(Roles = "ComplianceOfficer,Manager,Admin")]
    public async Task<IActionResult> GenerateComplianceReportAsync(
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (fromDate >= toDate)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid Date Range",
                    Detail = "From date must be earlier than to date",
                    Status = (int)HttpStatusCode.BadRequest
                });
            }

            if ((toDate - fromDate).TotalDays > 365)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Date Range Too Large",
                    Detail = "Date range cannot exceed 365 days",
                    Status = (int)HttpStatusCode.BadRequest
                });
            }

            var report = await _kycDocumentService.GenerateComplianceReportAsync(fromDate, toDate, cancellationToken);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating compliance report for period {FromDate} to {ToDate}", 
                fromDate, toDate);
            
            return Problem(
                title: "Compliance Report Error",
                detail: "An error occurred while generating the compliance report",
                statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    [HttpDelete("{id}")]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.NotFound)]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteDocumentAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            var deletedBy = User.FindFirst("sub")?.Value ?? "system";
            var success = await _kycDocumentService.DeleteDocumentAsync(id, deletedBy, cancellationToken);

            if (!success)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Document Not Found",
                    Detail = $"Document with ID {id} was not found",
                    Status = (int)HttpStatusCode.NotFound
                });
            }

            _logger.LogInformation("Document {DocumentId} deleted by {DeletedBy}", id, deletedBy);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document {DocumentId}", id);
            return Problem(
                title: "Document Deletion Error",
                detail: "An error occurred while deleting the document",
                statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }
}

public class ApprovalRequest
{
    public string? Notes { get; set; }
}

public class RejectionRequest
{
    public string Reason { get; set; } = string.Empty;
}