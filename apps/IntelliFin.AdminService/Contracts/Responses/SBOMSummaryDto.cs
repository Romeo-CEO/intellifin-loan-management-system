namespace IntelliFin.AdminService.Contracts.Responses;

public sealed record SBOMSummaryDto(
    string ServiceName,
    string Version,
    string ImageDigest,
    bool IsSigned,
    bool SignatureVerified,
    bool HasSbom,
    DateTime BuildTimestamp,
    int CriticalCount,
    int HighCount,
    int MediumCount,
    int LowCount);
