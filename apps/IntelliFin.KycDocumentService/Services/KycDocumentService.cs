using IntelliFin.KycDocumentService.Models;
using IntelliFin.Shared.DomainModels.Data;
using IntelliFin.Shared.DomainModels.Entities;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text.Json;

namespace IntelliFin.KycDocumentService.Services;

public class KycDocumentService : IKycDocumentService
{
    private readonly IDocumentStorageService _storageService;
    private readonly IDocumentValidationService _validationService;
    private readonly LmsDbContext _context;
    private readonly ILogger<KycDocumentService> _logger;

    public KycDocumentService(
        IDocumentStorageService storageService,
        IDocumentValidationService validationService,
        LmsDbContext context,
        ILogger<KycDocumentService> logger)
    {
        _storageService = storageService;
        _validationService = validationService;
        _context = context;
        _logger = logger;
    }

    public async Task<DocumentUploadResponse> UploadDocumentAsync(DocumentUploadRequest request, string uploadedBy,
        CancellationToken cancellationToken = default)
    {
        using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        
        try
        {
            _logger.LogInformation("Starting document upload for client {ClientId}, type {DocumentType}", 
                request.ClientId, request.DocumentType);

            // Validate client exists
            var clientExists = await _context.Clients
                .AnyAsync(c => c.Id.ToString() == request.ClientId, cancellationToken);
            
            if (!clientExists)
            {
                throw new KeyNotFoundException($"Client with ID {request.ClientId} not found");
            }

            // Validate file format
            var contentType = request.File.ContentType;
            var isValidType = await _validationService.IsDocumentTypeValidAsync(request.DocumentType, contentType);
            
            if (!isValidType)
            {
                throw new ArgumentException($"File type {contentType} is not supported for document type {request.DocumentType}");
            }

            // Create document record
            var document = new KycDocument
            {
                Id = Guid.NewGuid().ToString(),
                ClientId = request.ClientId,
                DocumentType = request.DocumentType,
                FileName = request.File.FileName,
                ContentType = contentType,
                FileSize = request.File.Length,
                Status = KycDocumentStatus.Uploaded,
                UploadedAt = DateTime.UtcNow,
                UploadedBy = uploadedBy,
                ExpiresAt = request.ExpirationDate,
                Metadata = new Dictionary<string, object>
                {
                    ["description"] = request.Description ?? "",
                    ["auto_verify"] = request.AutoVerify,
                    ["upload_source"] = "web_api"
                }
            };

            // Calculate file hash
            using var stream = request.File.OpenReadStream();
            document.FileHash = await CalculateFileHashAsync(stream);
            stream.Position = 0;

            // Store file
            var storagePath = $"kyc-documents/{request.ClientId}/{document.Id}/{request.File.FileName}";
            document.FilePath = await _storageService.SaveDocumentAsync(storagePath, stream, contentType, cancellationToken);

            // Save document record
            var documentVerification = new DocumentVerification
            {
                Id = Guid.NewGuid(),
                ClientId = Guid.Parse(request.ClientId),
                DocumentType = request.DocumentType.ToString(),
                DocumentNumber = "", // Will be populated after OCR
                DocumentImagePath = document.FilePath,
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };

            _context.DocumentVerifications.Add(documentVerification);
            await _context.SaveChangesAsync(cancellationToken);

            // Start validation process if auto-verify is enabled
            var requiresManualReview = false;
            if (request.AutoVerify)
            {
                try
                {
                    stream.Position = 0;
                    var validationResult = await _validationService.ValidateDocumentAsync(
                        document.Id, stream, request.DocumentType, cancellationToken);

                    document.ConfidenceScore = validationResult.ConfidenceScore;
                    document.ExtractedData = validationResult.ExtractedData;
                    requiresManualReview = validationResult.RequiresManualReview;
                    
                    if (validationResult.IsValid && !requiresManualReview)
                    {
                        document.Status = KycDocumentStatus.Approved;
                        document.VerifiedAt = DateTime.UtcNow;
                        document.VerifiedBy = "system_auto_verification";
                        
                        // Update DocumentVerification
                        documentVerification.OcrExtractedData = JsonSerializer.Serialize(validationResult.ExtractedData);
                        documentVerification.OcrConfidenceScore = (decimal)validationResult.ConfidenceScore;
                        documentVerification.OcrProvider = validationResult.ProcessorUsed ?? "DocumentValidationService";
                        documentVerification.IsVerified = true;
                        documentVerification.VerifiedBy = "system_auto_verification";
                        documentVerification.VerificationDate = DateTime.UtcNow;
                        documentVerification.VerificationDecisionReason = "Automatic verification based on confidence score";
                        
                        _logger.LogInformation("Document {DocumentId} auto-approved with confidence {Confidence}", 
                            document.Id, validationResult.ConfidenceScore);
                    }
                    else
                    {
                        document.Status = KycDocumentStatus.PendingReview;
                        documentVerification.OcrExtractedData = JsonSerializer.Serialize(validationResult.ExtractedData);
                        documentVerification.OcrConfidenceScore = (decimal)validationResult.ConfidenceScore;
                        documentVerification.OcrProvider = validationResult.ProcessorUsed ?? "DocumentValidationService";
                        
                        if (validationResult.Errors.Any())
                        {
                            documentVerification.DataMismatches = JsonSerializer.Serialize(validationResult.Errors);
                            documentVerification.HasDataMismatches = true;
                        }
                        
                        _logger.LogInformation("Document {DocumentId} requires manual review. Confidence: {Confidence}", 
                            document.Id, validationResult.ConfidenceScore);
                    }
                    
                    document.LastProcessedAt = DateTime.UtcNow;
                    document.ProcessingAttempts = 1;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during auto-validation of document {DocumentId}", document.Id);
                    document.Status = KycDocumentStatus.PendingReview;
                    requiresManualReview = true;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var response = new DocumentUploadResponse
            {
                DocumentId = document.Id,
                FileName = document.FileName,
                FileSize = document.FileSize,
                Status = document.Status,
                UploadedAt = document.UploadedAt,
                RequiresManualReview = requiresManualReview,
                Message = GetUploadMessage(document.Status, requiresManualReview)
            };

            _logger.LogInformation("Document uploaded successfully: {DocumentId} for client {ClientId}", 
                document.Id, request.ClientId);

            return response;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Error uploading document for client {ClientId}", request.ClientId);
            throw;
        }
    }

    public async Task<KycDocument?> GetDocumentAsync(string documentId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving document {DocumentId}", documentId);
        
        // Note: Since we're using a simplified model for the service layer, we'll create a mock response
        // In production, you'd query from a dedicated KYC documents table or use the DocumentVerification entity
        
        var verification = await _context.DocumentVerifications
            .FirstOrDefaultAsync(d => d.Id.ToString() == documentId, cancellationToken);

        if (verification == null) return null;

        return new KycDocument
        {
            Id = verification.Id.ToString(),
            ClientId = verification.ClientId.ToString(),
            DocumentType = Enum.TryParse<KycDocumentType>(verification.DocumentType, out var docType) ? docType : KycDocumentType.Other,
            FileName = Path.GetFileName(verification.DocumentImagePath),
            FilePath = verification.DocumentImagePath,
            ContentType = "image/jpeg", // Default - would be stored in actual implementation
            Status = verification.IsVerified ? KycDocumentStatus.Approved : KycDocumentStatus.PendingReview,
            UploadedAt = verification.CreatedAt,
            UploadedBy = "system", // Would be stored in actual implementation
            VerifiedAt = verification.VerificationDate,
            VerifiedBy = verification.VerifiedBy,
            ConfidenceScore = (float?)verification.OcrConfidenceScore,
            ExtractedData = string.IsNullOrEmpty(verification.OcrExtractedData) 
                ? new Dictionary<string, object>() 
                : JsonSerializer.Deserialize<Dictionary<string, object>>(verification.OcrExtractedData) ?? new Dictionary<string, object>()
        };
    }

    public async Task<IEnumerable<KycDocument>> GetClientDocumentsAsync(string clientId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving documents for client {ClientId}", clientId);

        var clientGuid = Guid.Parse(clientId);
        var verifications = await _context.DocumentVerifications
            .Where(d => d.ClientId == clientGuid)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);

        return verifications.Select(v => new KycDocument
        {
            Id = v.Id.ToString(),
            ClientId = v.ClientId.ToString(),
            DocumentType = Enum.TryParse<KycDocumentType>(v.DocumentType, out var docType) ? docType : KycDocumentType.Other,
            FileName = Path.GetFileName(v.DocumentImagePath),
            FilePath = v.DocumentImagePath,
            Status = v.IsVerified ? KycDocumentStatus.Approved : KycDocumentStatus.PendingReview,
            UploadedAt = v.CreatedAt,
            VerifiedAt = v.VerificationDate,
            VerifiedBy = v.VerifiedBy,
            ConfidenceScore = (float?)v.OcrConfidenceScore
        });
    }

    public async Task<DocumentValidationResult> ValidateDocumentAsync(string documentId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Validating document {DocumentId}", documentId);

        var verification = await _context.DocumentVerifications
            .FirstOrDefaultAsync(d => d.Id.ToString() == documentId, cancellationToken);

        if (verification == null)
        {
            throw new KeyNotFoundException($"Document with ID {documentId} not found");
        }

        try
        {
            using var documentStream = await _storageService.GetDocumentAsync(verification.DocumentImagePath, cancellationToken);
            var docType = Enum.TryParse<KycDocumentType>(verification.DocumentType, out var parsedType) ? parsedType : KycDocumentType.Other;
            
            var validationResult = await _validationService.ValidateDocumentAsync(
                documentId, documentStream, docType, cancellationToken);

            // Update verification record with new results
            verification.OcrExtractedData = JsonSerializer.Serialize(validationResult.ExtractedData);
            verification.OcrConfidenceScore = (decimal)validationResult.ConfidenceScore;
            verification.LastModified = DateTime.UtcNow;

            if (validationResult.Errors.Any())
            {
                verification.DataMismatches = JsonSerializer.Serialize(validationResult.Errors);
                verification.HasDataMismatches = true;
            }

            await _context.SaveChangesAsync(cancellationToken);

            return validationResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating document {DocumentId}", documentId);
            throw;
        }
    }

    public async Task<bool> ApproveDocumentAsync(string documentId, string approvedBy, string? notes = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Approving document {DocumentId} by {ApprovedBy}", documentId, approvedBy);

        var verification = await _context.DocumentVerifications
            .FirstOrDefaultAsync(d => d.Id.ToString() == documentId, cancellationToken);

        if (verification == null) return false;

        verification.IsVerified = true;
        verification.VerifiedBy = approvedBy;
        verification.VerificationDate = DateTime.UtcNow;
        verification.VerificationNotes = notes ?? "";
        verification.VerificationDecisionReason = "Manual approval by authorized personnel";
        verification.LastModified = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Document {DocumentId} approved by {ApprovedBy}", documentId, approvedBy);
        return true;
    }

    public async Task<bool> RejectDocumentAsync(string documentId, string rejectedBy, string reason,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Rejecting document {DocumentId} by {RejectedBy}", documentId, rejectedBy);

        var verification = await _context.DocumentVerifications
            .FirstOrDefaultAsync(d => d.Id.ToString() == documentId, cancellationToken);

        if (verification == null) return false;

        verification.IsVerified = false;
        verification.VerifiedBy = rejectedBy;
        verification.VerificationDate = DateTime.UtcNow;
        verification.VerificationDecisionReason = reason;
        verification.LastModified = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Document {DocumentId} rejected by {RejectedBy} with reason: {Reason}", 
            documentId, rejectedBy, reason);
        return true;
    }

    public async Task<string> GetDocumentDownloadUrlAsync(string documentId, TimeSpan expiry,
        CancellationToken cancellationToken = default)
    {
        var verification = await _context.DocumentVerifications
            .FirstOrDefaultAsync(d => d.Id.ToString() == documentId, cancellationToken);

        if (verification == null)
        {
            throw new KeyNotFoundException($"Document with ID {documentId} not found");
        }

        return await _storageService.GenerateDownloadUrlAsync(verification.DocumentImagePath, expiry, cancellationToken);
    }

    public async Task<bool> DeleteDocumentAsync(string documentId, string deletedBy, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting document {DocumentId} by {DeletedBy}", documentId, deletedBy);

        var verification = await _context.DocumentVerifications
            .FirstOrDefaultAsync(d => d.Id.ToString() == documentId, cancellationToken);

        if (verification == null) return false;

        try
        {
            // Delete from storage first
            await _storageService.DeleteDocumentAsync(verification.DocumentImagePath, cancellationToken);

            // Remove from database
            _context.DocumentVerifications.Remove(verification);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Document {DocumentId} deleted successfully by {DeletedBy}", documentId, deletedBy);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document {DocumentId}", documentId);
            throw;
        }
    }

    public async Task<IEnumerable<KycDocument>> GetDocumentsForReviewAsync(int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.DocumentVerifications
            .Where(d => !d.IsVerified)
            .OrderBy(d => d.CreatedAt);

        if (limit.HasValue)
        {
            query = (IOrderedQueryable<DocumentVerification>)query.Take(limit.Value);
        }

        var verifications = await query.ToListAsync(cancellationToken);

        return verifications.Select(v => new KycDocument
        {
            Id = v.Id.ToString(),
            ClientId = v.ClientId.ToString(),
            DocumentType = Enum.TryParse<KycDocumentType>(v.DocumentType, out var docType) ? docType : KycDocumentType.Other,
            FileName = Path.GetFileName(v.DocumentImagePath),
            Status = KycDocumentStatus.PendingReview,
            UploadedAt = v.CreatedAt,
            ConfidenceScore = (float?)v.OcrConfidenceScore
        });
    }

    public async Task<ComplianceReport> GenerateComplianceReportAsync(DateTime fromDate, DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        var documents = await _context.DocumentVerifications
            .Where(d => d.CreatedAt >= fromDate && d.CreatedAt <= toDate)
            .ToListAsync(cancellationToken);

        var totalDocuments = documents.Count;
        var approvedDocuments = documents.Count(d => d.IsVerified);
        var rejectedDocuments = documents.Count(d => d.VerificationDate.HasValue && !d.IsVerified);
        var pendingDocuments = totalDocuments - approvedDocuments - rejectedDocuments;

        var documentsByType = documents
            .GroupBy(d => Enum.TryParse<KycDocumentType>(d.DocumentType, out var docType) ? docType : KycDocumentType.Other)
            .ToDictionary(g => g.Key, g => g.Count());

        var rejectionReasons = documents
            .Where(d => !string.IsNullOrEmpty(d.VerificationDecisionReason) && !d.IsVerified)
            .GroupBy(d => d.VerificationDecisionReason)
            .ToDictionary(g => g.Key, g => g.Count());

        var averageProcessingTime = 0f;
        var processedDocuments = documents.Where(d => d.VerificationDate.HasValue).ToList();
        if (processedDocuments.Any())
        {
            averageProcessingTime = (float)processedDocuments
                .Average(d => (d.VerificationDate!.Value - d.CreatedAt).TotalHours);
        }

        return new ComplianceReport
        {
            FromDate = fromDate,
            ToDate = toDate,
            TotalDocuments = totalDocuments,
            ApprovedDocuments = approvedDocuments,
            RejectedDocuments = rejectedDocuments,
            PendingDocuments = pendingDocuments,
            ComplianceRate = totalDocuments > 0 ? (float)approvedDocuments / totalDocuments * 100 : 0,
            DocumentsByType = documentsByType,
            RejectionReasons = rejectionReasons,
            AverageProcessingTimeHours = averageProcessingTime
        };
    }

    public async Task<int> CleanupExpiredDocumentsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting cleanup of expired documents");

        var expiredDocuments = await _context.DocumentVerifications
            .Where(d => d.CreatedAt < DateTime.UtcNow.AddYears(-10)) // 10-year retention policy
            .ToListAsync(cancellationToken);

        var count = 0;
        foreach (var doc in expiredDocuments)
        {
            try
            {
                await _storageService.DeleteDocumentAsync(doc.DocumentImagePath, cancellationToken);
                _context.DocumentVerifications.Remove(doc);
                count++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up expired document {DocumentId}", doc.Id);
            }
        }

        if (count > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("Cleaned up {Count} expired documents", count);
        return count;
    }

    public async Task<DocumentStatistics> GetDocumentStatisticsAsync(DateTime? fromDate = null, DateTime? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var query = _context.DocumentVerifications.AsQueryable();

        if (fromDate.HasValue)
            query = query.Where(d => d.CreatedAt >= fromDate);
        if (toDate.HasValue)
            query = query.Where(d => d.CreatedAt <= toDate);

        var documents = await query.ToListAsync(cancellationToken);

        var totalDocuments = documents.Count;
        var documentsToday = documents.Count(d => d.CreatedAt.Date == now.Date);
        var documentsThisWeek = documents.Count(d => d.CreatedAt >= now.AddDays(-7));
        var documentsThisMonth = documents.Count(d => d.CreatedAt >= now.AddMonths(-1));
        var pendingReview = documents.Count(d => !d.IsVerified && !d.VerificationDate.HasValue);
        var requireManualReview = documents.Count(d => d.OcrConfidenceScore < 0.7m);

        var averageConfidence = documents.Any(d => d.OcrConfidenceScore > 0)
            ? (float)documents.Where(d => d.OcrConfidenceScore > 0).Average(d => d.OcrConfidenceScore)
            : 0f;

        var documentsByType = documents
            .GroupBy(d => Enum.TryParse<KycDocumentType>(d.DocumentType, out var docType) ? docType : KycDocumentType.Other)
            .ToDictionary(g => g.Key, g => g.Count());

        var documentsByStatus = documents
            .GroupBy(d => d.IsVerified ? KycDocumentStatus.Approved : 
                        d.VerificationDate.HasValue ? KycDocumentStatus.Rejected : KycDocumentStatus.PendingReview)
            .ToDictionary(g => g.Key, g => g.Count());

        return new DocumentStatistics
        {
            TotalDocuments = totalDocuments,
            DocumentsToday = documentsToday,
            DocumentsThisWeek = documentsThisWeek,
            DocumentsThisMonth = documentsThisMonth,
            PendingReview = pendingReview,
            RequireManualReview = requireManualReview,
            AverageConfidenceScore = averageConfidence,
            DocumentsByType = documentsByType,
            DocumentsByStatus = documentsByStatus
        };
    }

    private async Task<string> CalculateFileHashAsync(Stream stream)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = await Task.Run(() => sha256.ComputeHash(stream));
        return Convert.ToBase64String(hashBytes);
    }

    private string GetUploadMessage(KycDocumentStatus status, bool requiresManualReview)
    {
        return status switch
        {
            KycDocumentStatus.Approved => "Document uploaded and automatically approved",
            KycDocumentStatus.PendingReview when requiresManualReview => "Document uploaded successfully and queued for manual review",
            KycDocumentStatus.PendingReview => "Document uploaded successfully and is being processed",
            _ => "Document uploaded successfully"
        };
    }
}