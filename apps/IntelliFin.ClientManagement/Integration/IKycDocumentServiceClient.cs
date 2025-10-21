using IntelliFin.ClientManagement.Integration.DTOs;
using Refit;

namespace IntelliFin.ClientManagement.Integration;

/// <summary>
/// Refit interface for KycDocumentService API
/// Handles document upload/download/metadata operations via HTTP
/// </summary>
public interface IKycDocumentServiceClient
{
    /// <summary>
    /// Uploads a document to MinIO via KycDocumentService
    /// </summary>
    /// <param name="content">Multipart form data containing file and metadata</param>
    /// <returns>Upload response with ObjectKey, hash, and document ID</returns>
    [Post("/api/documents")]
    Task<UploadDocumentResponse> UploadDocumentAsync([Body] StreamPart content);

    /// <summary>
    /// Retrieves document metadata from KycDocumentService
    /// </summary>
    /// <param name="documentId">Unique document identifier</param>
    /// <returns>Document metadata including storage details and status</returns>
    [Get("/api/documents/{documentId}")]
    Task<DocumentMetadataResponse> GetDocumentMetadataAsync(Guid documentId);

    /// <summary>
    /// Generates a pre-signed download URL for secure document access
    /// </summary>
    /// <param name="documentId">Unique document identifier</param>
    /// <returns>Pre-signed URL valid for 1 hour</returns>
    [Get("/api/documents/{documentId}/download")]
    Task<DownloadUrlResponse> GetDownloadUrlAsync(Guid documentId);
}
