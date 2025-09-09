using System.ComponentModel.DataAnnotations;

namespace IntelliFin.KycDocumentService.Models;

public class KycDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ClientId { get; set; } = string.Empty;
    public KycDocumentType DocumentType { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string FileHash { get; set; } = string.Empty;
    public KycDocumentStatus Status { get; set; } = KycDocumentStatus.Uploaded;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public string UploadedBy { get; set; } = string.Empty;
    public DateTime? VerifiedAt { get; set; }
    public string? VerifiedBy { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? RejectionReason { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    public Dictionary<string, object> ExtractedData { get; set; } = new();
    public float? ConfidenceScore { get; set; }
    public int ProcessingAttempts { get; set; } = 0;
    public DateTime? LastProcessedAt { get; set; }
}

public enum KycDocumentType
{
    NationalId = 1,
    Passport = 2,
    DriversLicense = 3,
    ProofOfAddress = 4,
    PaySlip = 5,
    BankStatement = 6,
    EmploymentLetter = 7,
    TaxCertificate = 8,
    Other = 99
}

public enum KycDocumentStatus
{
    Uploaded = 0,
    Processing = 1,
    PendingReview = 2,
    Approved = 3,
    Rejected = 4,
    Expired = 5,
    Archived = 6
}