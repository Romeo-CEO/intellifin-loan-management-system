using IntelliFin.ClientManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace IntelliFin.ClientManagement.Controllers;

/// <summary>
/// API controller for client document management
/// Handles document upload, retrieval, and download URL generation
/// </summary>
[ApiController]
[Route("api/clients/{clientId:guid}/documents")]
[Authorize]
public class ClientDocumentController : ControllerBase
{
    private readonly IDocumentLifecycleService _documentService;
    private readonly ILogger<ClientDocumentController> _logger;

    public ClientDocumentController(
        IDocumentLifecycleService documentService,
        ILogger<ClientDocumentController> logger)
    {
        _documentService = documentService;
        _logger = logger;
    }

    /// <summary>
    /// Uploads a document for a client
    /// </summary>
    /// <param name="clientId">Client unique identifier</param>
    /// <param name="file">File to upload (PDF, JPG, PNG, max 10MB)</param>
    /// <param name="documentType">Type of document (NRC, Payslip, ProofOfResidence, etc.)</param>
    /// <param name="category">Category (KYC, Loan, Compliance, General)</param>
    /// <returns>Document metadata</returns>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(Integration.DTOs.DocumentMetadataResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status413RequestEntityTooLarge)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UploadDocument(
        Guid clientId,
        [FromForm] IFormFile file,
        [FromForm] string documentType,
        [FromForm] string category = "General")
    {
        var userId = GetUserId();
        var correlationId = GetCorrelationId();

        _logger.LogInformation(
            "Document upload request: ClientId={ClientId}, DocumentType={DocumentType}, Category={Category}, User={UserId}",
            clientId, documentType, category, userId);

        var result = await _documentService.UploadDocumentAsync(
            clientId, file, documentType, category, userId, correlationId);

        if (result.IsFailure)
        {
            if (result.Error.Contains("not found"))
            {
                return NotFound(new { error = result.Error });
            }

            if (result.Error.Contains("exceeds maximum") || result.Error.Contains("Invalid"))
            {
                return BadRequest(new { error = result.Error });
            }

            return StatusCode(500, new { error = result.Error });
        }

        return CreatedAtAction(
            nameof(GetDocumentMetadata),
            new { clientId, documentId = result.Value!.Id },
            result.Value);
    }

    /// <summary>
    /// Lists all documents for a client
    /// </summary>
    /// <param name="clientId">Client unique identifier</param>
    /// <returns>List of document metadata</returns>
    [HttpGet]
    [ProducesResponseType(typeof(List<Integration.DTOs.DocumentMetadataResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListDocuments(Guid clientId)
    {
        var result = await _documentService.ListDocumentsAsync(clientId);

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
    /// Gets metadata for a specific document
    /// </summary>
    /// <param name="clientId">Client unique identifier</param>
    /// <param name="documentId">Document unique identifier</param>
    /// <returns>Document metadata</returns>
    [HttpGet("{documentId:guid}")]
    [ProducesResponseType(typeof(Integration.DTOs.DocumentMetadataResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDocumentMetadata(Guid clientId, Guid documentId)
    {
        var result = await _documentService.GetDocumentMetadataAsync(clientId, documentId);

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
    /// Generates a pre-signed download URL for a document
    /// </summary>
    /// <param name="clientId">Client unique identifier</param>
    /// <param name="documentId">Document unique identifier</param>
    /// <returns>Pre-signed download URL (valid for 1 hour)</returns>
    [HttpGet("{documentId:guid}/download")]
    [ProducesResponseType(typeof(Integration.DTOs.DownloadUrlResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetDownloadUrl(Guid clientId, Guid documentId)
    {
        var userId = GetUserId();

        var result = await _documentService.GenerateDownloadUrlAsync(clientId, documentId, userId);

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
