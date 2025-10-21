using IntelliFin.ClientManagement.Common;
using IntelliFin.ClientManagement.Controllers.DTOs;
using IntelliFin.ClientManagement.Domain.Entities;
using IntelliFin.ClientManagement.Domain.Enums;
using IntelliFin.ClientManagement.Domain.Exceptions;
using IntelliFin.ClientManagement.Infrastructure.Persistence;
using IntelliFin.ClientManagement.Integration;
using IntelliFin.ClientManagement.Integration.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Refit;

namespace IntelliFin.ClientManagement.Services;

/// <summary>
/// Service implementation for document lifecycle management
/// Orchestrates document upload/download via KycDocumentService and metadata storage
/// </summary>
public class DocumentLifecycleService : IDocumentLifecycleService
{
    private readonly ClientManagementDbContext _context;
    private readonly IKycDocumentServiceClient _kycDocumentClient;
    private readonly IAuditService _auditService;
    private readonly ILogger<DocumentLifecycleService> _logger;
    private const int RetentionYears = 7; // BoZ compliance requirement

    public DocumentLifecycleService(
        ClientManagementDbContext context,
        IKycDocumentServiceClient kycDocumentClient,
        IAuditService auditService,
        ILogger<DocumentLifecycleService> logger)
    {
        _context = context;
        _kycDocumentClient = kycDocumentClient;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<Result<DocumentMetadataResponse>> UploadDocumentAsync(
        Guid clientId,
        IFormFile file,
        string documentType,
        string category,
        string userId,
        string? correlationId = null)
    {
        try
        {
            // Verify client exists
            var clientExists = await _context.Clients.AnyAsync(c => c.Id == clientId);
            if (!clientExists)
            {
                _logger.LogWarning("Client not found: {ClientId}", clientId);
                return Result<DocumentMetadataResponse>.Failure($"Client with ID {clientId} not found");
            }

            // Validate file
            var fileValidation = DocumentValidator.ValidateFile(file);
            if (fileValidation.IsFailure)
            {
                _logger.LogWarning("File validation failed for client {ClientId}: {Error}", clientId, fileValidation.Error);
                return Result<DocumentMetadataResponse>.Failure(fileValidation.Error);
            }

            // Validate document type
            var typeValidation = DocumentValidator.ValidateDocumentType(documentType);
            if (typeValidation.IsFailure)
            {
                return Result<DocumentMetadataResponse>.Failure(typeValidation.Error);
            }

            // Validate category
            var categoryValidation = DocumentValidator.ValidateCategory(category);
            if (categoryValidation.IsFailure)
            {
                return Result<DocumentMetadataResponse>.Failure(categoryValidation.Error);
            }

            // Calculate SHA256 hash
            string fileHash;
            using (var stream = file.OpenReadStream())
            {
                fileHash = await DocumentValidator.CalculateSha256HashAsync(stream);
            }

            _logger.LogInformation(
                "Uploading document for client {ClientId}: Type={DocumentType}, Size={FileSize}, Hash={Hash}",
                clientId, documentType, file.Length, fileHash);

            // Upload to KycDocumentService
            UploadDocumentResponse uploadResponse;
            try
            {
                using var fileStream = file.OpenReadStream();
                var streamPart = new StreamPart(fileStream, file.FileName, file.ContentType);
                uploadResponse = await _kycDocumentClient.UploadDocumentAsync(streamPart);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload document to KycDocumentService for client {ClientId}", clientId);
                return Result<DocumentMetadataResponse>.Failure(
                    "Failed to upload document to storage service. Please try again.");
            }

            // Create ClientDocument entity
            var document = new ClientDocument
            {
                Id = uploadResponse.DocumentId,
                ClientId = clientId,
                DocumentType = documentType,
                Category = category,
                ObjectKey = uploadResponse.ObjectKey,
                BucketName = uploadResponse.BucketName,
                FileName = file.FileName,
                ContentType = file.ContentType,
                FileSizeBytes = file.Length,
                FileHashSha256 = fileHash,
                UploadStatus = UploadStatus.Uploaded, // Initial status - awaiting verification
                UploadedAt = DateTime.UtcNow,
                UploadedBy = userId,
                RetentionUntil = DateTime.UtcNow.AddYears(RetentionYears), // BoZ 7-year retention
                CreatedAt = DateTime.UtcNow,
                CorrelationId = correlationId,
                IsArchived = false
            };

            _context.ClientDocuments.Add(document);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Document uploaded successfully: DocumentId={DocumentId}, ClientId={ClientId}, Type={DocumentType}",
                document.Id, clientId, documentType);

            // Log audit event (fire-and-forget)
            await _auditService.LogAuditEventAsync(
                action: "DocumentUploaded",
                entityType: "ClientDocument",
                entityId: document.Id.ToString(),
                actor: userId,
                eventData: new
                {
                    ClientId = clientId,
                    DocumentType = documentType,
                    Category = category,
                    FileName = file.FileName,
                    FileSizeBytes = file.Length,
                    FileHash = fileHash
                });

            // Map to response DTO
            return Result<DocumentMetadataResponse>.Success(MapToMetadataResponse(document));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading document for client {ClientId}", clientId);
            return Result<DocumentMetadataResponse>.Failure($"Error uploading document: {ex.Message}");
        }
    }

    public async Task<Result<DocumentMetadataResponse>> GetDocumentMetadataAsync(
        Guid clientId,
        Guid documentId)
    {
        try
        {
            var document = await _context.ClientDocuments
                .FirstOrDefaultAsync(d => d.Id == documentId && d.ClientId == clientId);

            if (document == null)
            {
                _logger.LogWarning("Document not found: DocumentId={DocumentId}, ClientId={ClientId}", 
                    documentId, clientId);
                return Result<DocumentMetadataResponse>.Failure(
                    $"Document with ID {documentId} not found for client {clientId}");
            }

            return Result<DocumentMetadataResponse>.Success(MapToMetadataResponse(document));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving document metadata: DocumentId={DocumentId}", documentId);
            return Result<DocumentMetadataResponse>.Failure($"Error retrieving document metadata: {ex.Message}");
        }
    }

    public async Task<Result<DownloadUrlResponse>> GenerateDownloadUrlAsync(
        Guid clientId,
        Guid documentId,
        string userId)
    {
        try
        {
            // Verify document exists and belongs to client
            var document = await _context.ClientDocuments
                .FirstOrDefaultAsync(d => d.Id == documentId && d.ClientId == clientId);

            if (document == null)
            {
                _logger.LogWarning("Document not found for download: DocumentId={DocumentId}, ClientId={ClientId}", 
                    documentId, clientId);
                return Result<DownloadUrlResponse>.Failure(
                    $"Document with ID {documentId} not found for client {clientId}");
            }

            // Call KycDocumentService to generate pre-signed URL
            DownloadUrlResponse downloadResponse;
            try
            {
                downloadResponse = await _kycDocumentClient.GetDownloadUrlAsync(documentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate download URL from KycDocumentService: DocumentId={DocumentId}", 
                    documentId);
                return Result<DownloadUrlResponse>.Failure(
                    "Failed to generate download URL. Please try again.");
            }

            _logger.LogInformation(
                "Generated download URL for document: DocumentId={DocumentId}, ClientId={ClientId}, User={UserId}",
                documentId, clientId, userId);

            // Log audit event (fire-and-forget)
            await _auditService.LogAuditEventAsync(
                action: "DocumentDownloaded",
                entityType: "ClientDocument",
                entityId: documentId.ToString(),
                actor: userId,
                eventData: new
                {
                    ClientId = clientId,
                    DocumentType = document.DocumentType,
                    FileName = document.FileName
                });

            return Result<DownloadUrlResponse>.Success(downloadResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating download URL: DocumentId={DocumentId}", documentId);
            return Result<DownloadUrlResponse>.Failure($"Error generating download URL: {ex.Message}");
        }
    }

    public async Task<Result<List<DocumentMetadataResponse>>> ListDocumentsAsync(Guid clientId)
    {
        try
        {
            // Verify client exists
            var clientExists = await _context.Clients.AnyAsync(c => c.Id == clientId);
            if (!clientExists)
            {
                _logger.LogWarning("Client not found: {ClientId}", clientId);
                return Result<List<DocumentMetadataResponse>>.Failure($"Client with ID {clientId} not found");
            }

            var documents = await _context.ClientDocuments
                .Where(d => d.ClientId == clientId && !d.IsArchived)
                .OrderByDescending(d => d.UploadedAt)
                .ToListAsync();

            var response = documents.Select(MapToMetadataResponse).ToList();

            _logger.LogInformation("Retrieved {Count} documents for client {ClientId}", 
                response.Count, clientId);

            return Result<List<DocumentMetadataResponse>>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing documents for client {ClientId}", clientId);
            return Result<List<DocumentMetadataResponse>>.Failure($"Error listing documents: {ex.Message}");
        }
    }

    public async Task<Result<DocumentMetadataResponse>> VerifyDocumentAsync(
        Guid clientId,
        Guid documentId,
        VerifyDocumentRequest request,
        string userId)
    {
        try
        {
            // Load document
            var document = await _context.ClientDocuments
                .FirstOrDefaultAsync(d => d.Id == documentId && d.ClientId == clientId);

            if (document == null)
            {
                _logger.LogWarning("Document not found for verification: DocumentId={DocumentId}, ClientId={ClientId}",
                    documentId, clientId);
                return Result<DocumentMetadataResponse>.Failure(
                    $"Document with ID {documentId} not found for client {clientId}");
            }

            // Validate document is in Uploaded status
            if (document.UploadStatus != UploadStatus.Uploaded)
            {
                _logger.LogWarning(
                    "Cannot verify document in {Status} status: DocumentId={DocumentId}",
                    document.UploadStatus, documentId);
                return Result<DocumentMetadataResponse>.Failure(
                    $"Document cannot be verified in {document.UploadStatus} status. Only documents in Uploaded status can be verified.");
            }

            // CRITICAL: Dual-control enforcement - verifier must be different from uploader
            if (document.UploadedBy.Equals(userId, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Dual-control violation: User {UserId} attempted to verify document they uploaded: DocumentId={DocumentId}",
                    userId, documentId);
                
                throw new DualControlViolationException(userId, document.UploadedBy, documentId);
            }

            // Update document based on approval/rejection
            var now = DateTime.UtcNow;

            if (request.Approved)
            {
                // Approve document
                document.UploadStatus = UploadStatus.Verified;
                document.VerifiedBy = userId;
                document.VerifiedAt = now;
                document.RejectionReason = null; // Clear any previous rejection reason

                _logger.LogInformation(
                    "Document verified: DocumentId={DocumentId}, VerifiedBy={UserId}, UploadedBy={UploadedBy}",
                    documentId, userId, document.UploadedBy);
            }
            else
            {
                // Reject document
                document.UploadStatus = UploadStatus.Rejected;
                document.RejectionReason = request.RejectionReason;
                document.VerifiedBy = userId; // Track who rejected it
                document.VerifiedAt = now;

                _logger.LogInformation(
                    "Document rejected: DocumentId={DocumentId}, RejectedBy={UserId}, Reason={Reason}",
                    documentId, userId, request.RejectionReason);
            }

            await _context.SaveChangesAsync();

            // Log audit event (fire-and-forget)
            await _auditService.LogAuditEventAsync(
                action: request.Approved ? "DocumentVerified" : "DocumentRejected",
                entityType: "ClientDocument",
                entityId: documentId.ToString(),
                actor: userId,
                eventData: new
                {
                    DocumentId = documentId,
                    ClientId = clientId,
                    DocumentType = document.DocumentType,
                    VerifiedBy = userId,
                    UploadedBy = document.UploadedBy,
                    Approved = request.Approved,
                    RejectionReason = request.RejectionReason
                });

            return Result<DocumentMetadataResponse>.Success(MapToMetadataResponse(document));
        }
        catch (DualControlViolationException)
        {
            // Re-throw dual-control violations to be handled by controller
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying document: DocumentId={DocumentId}", documentId);
            return Result<DocumentMetadataResponse>.Failure($"Error verifying document: {ex.Message}");
        }
    }

    private static DocumentMetadataResponse MapToMetadataResponse(ClientDocument document)
    {
        return new DocumentMetadataResponse
        {
            Id = document.Id,
            ClientId = document.ClientId,
            DocumentType = document.DocumentType,
            Category = document.Category,
            ObjectKey = document.ObjectKey,
            BucketName = document.BucketName,
            FileName = document.FileName,
            ContentType = document.ContentType,
            FileSizeBytes = document.FileSizeBytes,
            FileHashSha256 = document.FileHashSha256,
            UploadStatus = document.UploadStatus.ToString(), // Convert enum to string for API
            UploadedAt = document.UploadedAt,
            UploadedBy = document.UploadedBy,
            VerifiedAt = document.VerifiedAt,
            VerifiedBy = document.VerifiedBy,
            ExpiryDate = document.ExpiryDate,
            RetentionUntil = document.RetentionUntil
        };
    }
}
