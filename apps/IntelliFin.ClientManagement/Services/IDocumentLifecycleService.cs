using IntelliFin.ClientManagement.Common;
using IntelliFin.ClientManagement.Controllers.DTOs;
using IntelliFin.ClientManagement.Integration.DTOs;
using Microsoft.AspNetCore.Http;

namespace IntelliFin.ClientManagement.Services;

/// <summary>
/// Service interface for document lifecycle management
/// Handles upload, retrieval, and download URL generation for client documents
/// </summary>
public interface IDocumentLifecycleService
{
    /// <summary>
    /// Uploads a document for a client and stores metadata
    /// </summary>
    /// <param name="clientId">Client unique identifier</param>
    /// <param name="file">File to upload</param>
    /// <param name="documentType">Type of document (NRC, Payslip, etc.)</param>
    /// <param name="category">Document category (KYC, Loan, Compliance, General)</param>
    /// <param name="userId">User performing the upload</param>
    /// <param name="correlationId">Correlation ID for tracing</param>
    /// <returns>Document metadata response</returns>
    Task<Result<DocumentMetadataResponse>> UploadDocumentAsync(
        Guid clientId,
        IFormFile file,
        string documentType,
        string category,
        string userId,
        string? correlationId = null);

    /// <summary>
    /// Retrieves metadata for a specific document
    /// </summary>
    /// <param name="clientId">Client unique identifier</param>
    /// <param name="documentId">Document unique identifier</param>
    /// <returns>Document metadata</returns>
    Task<Result<DocumentMetadataResponse>> GetDocumentMetadataAsync(
        Guid clientId,
        Guid documentId);

    /// <summary>
    /// Generates a pre-signed download URL for a document
    /// </summary>
    /// <param name="clientId">Client unique identifier</param>
    /// <param name="documentId">Document unique identifier</param>
    /// <param name="userId">User requesting the download</param>
    /// <returns>Pre-signed download URL</returns>
    Task<Result<DownloadUrlResponse>> GenerateDownloadUrlAsync(
        Guid clientId,
        Guid documentId,
        string userId);

    /// <summary>
    /// Lists all documents for a client
    /// </summary>
    /// <param name="clientId">Client unique identifier</param>
    /// <returns>List of document metadata</returns>
    Task<Result<List<DocumentMetadataResponse>>> ListDocumentsAsync(Guid clientId);

    /// <summary>
    /// Verifies or rejects a document (dual-control verification)
    /// Enforces dual-control: verifying user must be different from uploader
    /// </summary>
    /// <param name="clientId">Client unique identifier</param>
    /// <param name="documentId">Document unique identifier</param>
    /// <param name="request">Verification request (approved/rejected)</param>
    /// <param name="userId">User performing verification (must be different from uploader)</param>
    /// <returns>Updated document metadata</returns>
    Task<Result<DocumentMetadataResponse>> VerifyDocumentAsync(
        Guid clientId,
        Guid documentId,
        VerifyDocumentRequest request,
        string userId);
}
