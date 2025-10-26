namespace IntelliFin.ClientManagement.Domain.Enums;

/// <summary>
/// Document upload and verification status
/// Tracks the lifecycle of a document from upload through dual-control verification
/// </summary>
public enum UploadStatus
{
    /// <summary>
    /// Document has been uploaded and is awaiting verification
    /// Initial state after upload by first officer
    /// </summary>
    Uploaded = 1,

    /// <summary>
    /// Document is pending verification (reserved for Camunda workflow integration)
    /// Used when document is in verification workflow queue
    /// </summary>
    PendingVerification = 2,

    /// <summary>
    /// Document has been verified by a different officer (dual-control satisfied)
    /// Final approved state - document accepted for KYC purposes
    /// </summary>
    Verified = 3,

    /// <summary>
    /// Document has been rejected by verifier with reason provided
    /// Requires re-upload with corrections
    /// </summary>
    Rejected = 4
}
