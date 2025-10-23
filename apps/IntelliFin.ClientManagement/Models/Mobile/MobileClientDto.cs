namespace IntelliFin.ClientManagement.Models.Mobile;

/// <summary>
/// Lightweight client DTO optimized for mobile/tablet
/// Contains only essential fields for list views
/// </summary>
public class MobileClientSummary
{
    /// <summary>
    /// Client ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Full name
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// NRC number (masked for privacy: 111111/11/*)
    /// </summary>
    public string NrcMasked { get; set; } = string.Empty;

    /// <summary>
    /// Primary phone
    /// </summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>
    /// Branch ID
    /// </summary>
    public Guid BranchId { get; set; }

    /// <summary>
    /// KYC status (Pending, InProgress, Completed, Rejected, EDD_Required)
    /// </summary>
    public string KycStatus { get; set; } = string.Empty;

    /// <summary>
    /// Risk rating (Low, Medium, High)
    /// </summary>
    public string? RiskRating { get; set; }

    /// <summary>
    /// Created date
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Whether client has pending documents
    /// </summary>
    public bool HasPendingDocuments { get; set; }

    /// <summary>
    /// Document completion percentage (0-100)
    /// </summary>
    public int DocumentCompletionPercent { get; set; }
}

/// <summary>
/// Lightweight document DTO for mobile
/// </summary>
public class MobileDocumentSummary
{
    /// <summary>
    /// Document ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Document type
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Upload status
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Uploaded date
    /// </summary>
    public DateTime UploadedAt { get; set; }

    /// <summary>
    /// File size (human-readable: "1.2 MB")
    /// </summary>
    public string FileSize { get; set; } = string.Empty;

    /// <summary>
    /// Whether document has thumbnail
    /// </summary>
    public bool HasThumbnail { get; set; }

    /// <summary>
    /// Thumbnail URL (if available)
    /// </summary>
    public string? ThumbnailUrl { get; set; }
}

/// <summary>
/// Mobile dashboard summary
/// Aggregated stats for quick overview
/// </summary>
public class MobileDashboardSummary
{
    /// <summary>
    /// Total clients managed by officer
    /// </summary>
    public int TotalClients { get; set; }

    /// <summary>
    /// Clients pending KYC completion
    /// </summary>
    public int PendingKyc { get; set; }

    /// <summary>
    /// Documents pending verification
    /// </summary>
    public int PendingDocuments { get; set; }

    /// <summary>
    /// KYC processes completed today
    /// </summary>
    public int CompletedToday { get; set; }

    /// <summary>
    /// Clients requiring attention (EDD, rejections)
    /// </summary>
    public int RequiringAttention { get; set; }

    /// <summary>
    /// Recent clients (last 5)
    /// </summary>
    public List<MobileClientSummary> RecentClients { get; set; } = new();
}
