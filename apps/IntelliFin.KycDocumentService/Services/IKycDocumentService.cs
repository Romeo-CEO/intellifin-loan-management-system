using IntelliFin.KycDocumentService.Models;

namespace IntelliFin.KycDocumentService.Services;

public interface IKycDocumentService
{
    Task<DocumentUploadResponse> UploadDocumentAsync(DocumentUploadRequest request, string uploadedBy, 
        CancellationToken cancellationToken = default);
    
    Task<KycDocument?> GetDocumentAsync(string documentId, CancellationToken cancellationToken = default);
    
    Task<IEnumerable<KycDocument>> GetClientDocumentsAsync(string clientId, CancellationToken cancellationToken = default);
    
    Task<DocumentValidationResult> ValidateDocumentAsync(string documentId, CancellationToken cancellationToken = default);
    
    Task<bool> ApproveDocumentAsync(string documentId, string approvedBy, string? notes = null, 
        CancellationToken cancellationToken = default);
    
    Task<bool> RejectDocumentAsync(string documentId, string rejectedBy, string reason, 
        CancellationToken cancellationToken = default);
    
    Task<string> GetDocumentDownloadUrlAsync(string documentId, TimeSpan expiry, 
        CancellationToken cancellationToken = default);
    
    Task<bool> DeleteDocumentAsync(string documentId, string deletedBy, CancellationToken cancellationToken = default);
    
    Task<IEnumerable<KycDocument>> GetDocumentsForReviewAsync(int? limit = null, 
        CancellationToken cancellationToken = default);
    
    Task<ComplianceReport> GenerateComplianceReportAsync(DateTime fromDate, DateTime toDate, 
        CancellationToken cancellationToken = default);
    
    Task<int> CleanupExpiredDocumentsAsync(CancellationToken cancellationToken = default);
    
    Task<DocumentStatistics> GetDocumentStatisticsAsync(DateTime? fromDate = null, DateTime? toDate = null,
        CancellationToken cancellationToken = default);
}

public class ComplianceReport
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int TotalDocuments { get; set; }
    public int ApprovedDocuments { get; set; }
    public int RejectedDocuments { get; set; }
    public int PendingDocuments { get; set; }
    public float ComplianceRate { get; set; }
    public Dictionary<KycDocumentType, int> DocumentsByType { get; set; } = new();
    public Dictionary<string, int> RejectionReasons { get; set; } = new();
    public float AverageProcessingTimeHours { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

public class DocumentStatistics
{
    public int TotalDocuments { get; set; }
    public int DocumentsToday { get; set; }
    public int DocumentsThisWeek { get; set; }
    public int DocumentsThisMonth { get; set; }
    public int PendingReview { get; set; }
    public int RequireManualReview { get; set; }
    public float AverageConfidenceScore { get; set; }
    public Dictionary<KycDocumentType, int> DocumentsByType { get; set; } = new();
    public Dictionary<KycDocumentStatus, int> DocumentsByStatus { get; set; } = new();
}