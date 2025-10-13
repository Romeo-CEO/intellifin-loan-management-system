namespace IntelliFin.AdminService.Models;

public enum ChainStatus
{
    Valid,
    Broken,
    Tampered,
    Error
}

public sealed class ChainVerificationResult
{
    public ChainStatus Status { get; init; }
    public int EventsVerified { get; init; }
    public long? BrokenEventId { get; init; }
    public DateTime? BrokenEventTimestamp { get; init; }
    public int DurationMs { get; init; }
}

public sealed class AuditIntegrityStatus
{
    public AuditChainVerification? LastVerification { get; init; }
    public int TotalEvents { get; init; }
    public int VerifiedEvents { get; init; }
    public int BrokenEvents { get; init; }
    public double CoveragePercentage { get; init; }
}

public sealed class VerificationHistoryPage
{
    public required IReadOnlyList<AuditChainVerification> Items { get; init; }
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}
