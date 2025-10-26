namespace IntelliFin.ClientManagement.Domain.Entities;

/// <summary>
/// AML (Anti-Money Laundering) screening result for a client
/// Tracks sanctions, PEP (Politically Exposed Person), and watchlist checks
/// </summary>
public class AmlScreening
{
    /// <summary>
    /// Unique identifier for the screening record
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Foreign key to KycStatus entity
    /// </summary>
    public Guid KycStatusId { get; set; }

    /// <summary>
    /// Type of screening performed
    /// Values: "Sanctions", "PEP", "Watchlist"
    /// </summary>
    public string ScreeningType { get; set; } = string.Empty;

    /// <summary>
    /// Provider used for screening
    /// Values: "Manual", "TransUnion", "WorldCheck" (future)
    /// </summary>
    public string ScreeningProvider { get; set; } = "Manual";

    /// <summary>
    /// When screening was performed
    /// </summary>
    public DateTime ScreenedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User ID who performed screening
    /// </summary>
    public string ScreenedBy { get; set; } = string.Empty;

    /// <summary>
    /// Whether a match was found (hit)
    /// </summary>
    public bool IsMatch { get; set; }

    /// <summary>
    /// Details about the match (JSON format)
    /// Example: {"matchedName": "John Doe", "listName": "OFAC", "matchScore": 0.95}
    /// </summary>
    public string? MatchDetails { get; set; }

    /// <summary>
    /// Overall risk level from this screening
    /// Values: "Clear", "Low", "Medium", "High"
    /// </summary>
    public string RiskLevel { get; set; } = "Clear";

    /// <summary>
    /// Additional notes or comments
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Correlation ID for request tracking
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// When this record was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ========== Navigation Properties ==========

    /// <summary>
    /// Navigation property to KycStatus
    /// </summary>
    public KycStatus? KycStatus { get; set; }
}

/// <summary>
/// Static class for AML screening types
/// </summary>
public static class AmlScreeningType
{
    public const string Sanctions = "Sanctions";
    public const string PEP = "PEP";
    public const string Watchlist = "Watchlist";
}

/// <summary>
/// Static class for AML risk levels
/// </summary>
public static class AmlRiskLevel
{
    public const string Clear = "Clear";
    public const string Low = "Low";
    public const string Medium = "Medium";
    public const string High = "High";
}
