namespace IntelliFin.ClientManagement.Domain.Exceptions;

/// <summary>
/// Exception thrown when dual-control verification is violated
/// (user attempts to verify their own upload)
/// </summary>
public class DualControlViolationException : Exception
{
    /// <summary>
    /// User ID of the person attempting verification
    /// </summary>
    public string UserId { get; }

    /// <summary>
    /// User ID of the person who uploaded the document
    /// </summary>
    public string UploadedBy { get; }

    /// <summary>
    /// Document ID that was subject of the violation
    /// </summary>
    public Guid DocumentId { get; }

    /// <summary>
    /// Creates a new DualControlViolationException
    /// </summary>
    public DualControlViolationException(string userId, string uploadedBy, Guid documentId)
        : base($"Dual-control violation: User '{userId}' cannot verify document '{documentId}' they uploaded. A different officer must perform verification.")
    {
        UserId = userId;
        UploadedBy = uploadedBy;
        DocumentId = documentId;
    }

    /// <summary>
    /// Creates a new DualControlViolationException with custom message
    /// </summary>
    public DualControlViolationException(string userId, string uploadedBy, Guid documentId, string message)
        : base(message)
    {
        UserId = userId;
        UploadedBy = uploadedBy;
        DocumentId = documentId;
    }

    /// <summary>
    /// Creates a new DualControlViolationException with inner exception
    /// </summary>
    public DualControlViolationException(string userId, string uploadedBy, Guid documentId, string message, Exception innerException)
        : base(message, innerException)
    {
        UserId = userId;
        UploadedBy = uploadedBy;
        DocumentId = documentId;
    }
}
