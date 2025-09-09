using System.ComponentModel.DataAnnotations;

namespace IntelliFin.KycDocumentService.Models;

public class DocumentUploadRequest
{
    [Required(ErrorMessage = "Client ID is required")]
    public string ClientId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Document type is required")]
    public KycDocumentType DocumentType { get; set; }

    [Required(ErrorMessage = "File is required")]
    public IFormFile File { get; set; } = null!;

    public string? Description { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public bool AutoVerify { get; set; } = true;
}

public class DocumentUploadResponse
{
    public string DocumentId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public KycDocumentStatus Status { get; set; }
    public DateTime UploadedAt { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool RequiresManualReview { get; set; }
}