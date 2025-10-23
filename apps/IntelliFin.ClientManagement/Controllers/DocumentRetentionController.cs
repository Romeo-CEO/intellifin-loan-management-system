using IntelliFin.ClientManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntelliFin.ClientManagement.Controllers;

/// <summary>
/// API endpoints for document retention and archival management
/// Implements BoZ 10-year retention compliance
/// </summary>
[ApiController]
[Route("api/documents/retention")]
[Authorize]
public class DocumentRetentionController : ControllerBase
{
    private readonly IDocumentRetentionService _retentionService;
    private readonly ILogger<DocumentRetentionController> _logger;

    public DocumentRetentionController(
        IDocumentRetentionService retentionService,
        ILogger<DocumentRetentionController> logger)
    {
        _retentionService = retentionService;
        _logger = logger;
    }

    /// <summary>
    /// Gets retention statistics
    /// </summary>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(RetentionStatistics), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatistics()
    {
        var result = await _retentionService.GetRetentionStatisticsAsync();

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Gets documents eligible for archival
    /// </summary>
    [HttpGet("eligible")]
    [ProducesResponseType(typeof(List<DocumentRetentionInfo>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEligibleForArchival()
    {
        var result = await _retentionService.GetEligibleForArchivalAsync();

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Archives a specific document manually
    /// Requires compliance officer role
    /// </summary>
    /// <param name="documentId">Document ID to archive</param>
    /// <param name="request">Archival request</param>
    [HttpPost("{documentId}/archive")]
    [Authorize(Roles = "Compliance,Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ArchiveDocument(
        Guid documentId,
        [FromBody] ArchiveDocumentRequest request)
    {
        var userId = User.Identity?.Name ?? "unknown";

        var result = await _retentionService.ArchiveDocumentAsync(
            documentId,
            userId,
            request.Reason);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(new { success = true, message = "Document archived successfully" });
    }

    /// <summary>
    /// Restores a document from archive
    /// Requires compliance officer role
    /// </summary>
    /// <param name="documentId">Document ID to restore</param>
    [HttpPost("{documentId}/restore")]
    [Authorize(Roles = "Compliance,Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RestoreDocument(Guid documentId)
    {
        var userId = User.Identity?.Name ?? "unknown";

        var result = await _retentionService.RestoreDocumentAsync(documentId, userId);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(new { success = true, message = "Document restored successfully" });
    }

    /// <summary>
    /// Triggers manual archival process
    /// Requires admin role
    /// </summary>
    [HttpPost("archive-expired")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ArchiveExpiredDocuments()
    {
        _logger.LogInformation(
            "Manual archival triggered by {User}",
            User.Identity?.Name);

        var result = await _retentionService.ArchiveExpiredDocumentsAsync();

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(new
        {
            success = true,
            archivedCount = result.Value,
            message = $"Successfully archived {result.Value} documents"
        });
    }
}

/// <summary>
/// Request to archive a document
/// </summary>
public class ArchiveDocumentRequest
{
    /// <summary>
    /// Reason for manual archival
    /// </summary>
    public string Reason { get; set; } = "Manual archival by compliance officer";
}
