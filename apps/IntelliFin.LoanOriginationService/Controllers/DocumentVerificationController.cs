using IntelliFin.LoanOriginationService.Services;
using IntelliFin.Shared.DomainModels.Entities;
using Microsoft.AspNetCore.Mvc;

namespace IntelliFin.LoanOriginationService.Controllers;

/// <summary>
/// System-Assisted Manual Verification API
/// Implements the new KYC workflow without external API dependencies
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DocumentVerificationController : ControllerBase
{
    private readonly IDocumentIntelligenceService _documentIntelligenceService;
    private readonly ILogger<DocumentVerificationController> _logger;

    public DocumentVerificationController(
        IDocumentIntelligenceService documentIntelligenceService,
        ILogger<DocumentVerificationController> logger)
    {
        _documentIntelligenceService = documentIntelligenceService;
        _logger = logger;
    }

    /// <summary>
    /// Step 1: Upload document and trigger OCR processing
    /// </summary>
    [HttpPost("process-document")]
    public async Task<ActionResult<DocumentVerificationResponse>> ProcessDocumentAsync(
        [FromBody] DocumentVerificationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Processing document verification for client {ClientId}", request.ClientId);

            // Step 1: Extract data using OCR
            var ocrData = await _documentIntelligenceService.ExtractDocumentDataAsync(
                request.DocumentImagePath, request.DocumentType, cancellationToken);

            // Step 2: Compare with manually entered data
            var mismatches = await _documentIntelligenceService.CompareDataAsync(
                request.ManualData, ocrData, cancellationToken);

            // Step 3: Create verification record for loan officer review
            var verification = await _documentIntelligenceService.CreateVerificationRecordAsync(
                request.ClientId, request.DocumentType, request.DocumentImagePath,
                request.ManualData, ocrData, mismatches, cancellationToken);

            var response = new DocumentVerificationResponse
            {
                VerificationId = verification.Id,
                ClientId = verification.ClientId,
                DocumentType = verification.DocumentType,
                ManualData = request.ManualData,
                OcrData = ocrData,
                DataMismatches = mismatches,
                HasMismatches = mismatches.Any(),
                OcrConfidenceScore = ocrData.ConfidenceScore,
                Status = "PendingReview",
                RequiresLoanOfficerReview = true
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing document verification for client {ClientId}", request.ClientId);
            return StatusCode(500, new { error = "Failed to process document verification" });
        }
    }

    /// <summary>
    /// Step 2: Loan officer makes verification decision
    /// </summary>
    [HttpPost("complete-verification")]
    public async Task<ActionResult<DocumentVerification>> CompleteVerificationAsync(
        [FromBody] VerificationDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Completing verification {VerificationId} by {VerifiedBy}", 
                request.VerificationId, request.VerifiedBy);

            var verification = await _documentIntelligenceService.CompleteVerificationAsync(
                request.VerificationId, request.IsVerified, request.VerifiedBy,
                request.Notes, request.DecisionReason, cancellationToken);

            return Ok(verification);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "Verification record not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing verification {VerificationId}", request.VerificationId);
            return StatusCode(500, new { error = "Failed to complete verification" });
        }
    }

    /// <summary>
    /// Get all pending verifications for loan officer dashboard
    /// </summary>
    [HttpGet("pending")]
    public async Task<ActionResult<IEnumerable<DocumentVerification>>> GetPendingVerificationsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var pendingVerifications = await _documentIntelligenceService.GetPendingVerificationsAsync(cancellationToken);
            return Ok(pendingVerifications);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending verifications");
            return StatusCode(500, new { error = "Failed to retrieve pending verifications" });
        }
    }
}

/// <summary>
/// Response model for document verification processing
/// </summary>
public class DocumentVerificationResponse
{
    public Guid VerificationId { get; set; }
    public Guid ClientId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public ManualEntryData ManualData { get; set; } = new();
    public OcrDocumentData OcrData { get; set; } = new();
    public List<DataMismatch> DataMismatches { get; set; } = new();
    public bool HasMismatches { get; set; }
    public decimal OcrConfidenceScore { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool RequiresLoanOfficerReview { get; set; }
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}