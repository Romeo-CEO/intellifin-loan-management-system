namespace IntelliFin.AdminService.Contracts.Responses;

public sealed record ComplianceReportDto(
    DateTime GeneratedAt,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    IReadOnlyCollection<ComplianceReportImageDto> Images,
    int SignedImageCount,
    int TotalImageCount,
    int SbomAvailableCount,
    int CriticalVulnerabilityCount,
    IReadOnlyCollection<SignatureVerificationRecordDto> SignatureAuditTrail);

public sealed record ComplianceReportImageDto(
    string ServiceName,
    string Version,
    bool IsSigned,
    bool SignatureVerified,
    bool HasSbom,
    int CriticalCount,
    int HighCount,
    int MediumCount,
    int LowCount,
    DateTime BuildTimestamp,
    DateTime? DeploymentTimestamp);

public sealed record SignatureVerificationRecordDto(
    string ImageDigest,
    string ServiceName,
    string Version,
    DateTime VerificationTimestamp,
    string VerificationResult,
    string VerificationMethod,
    string? VerifiedBy,
    string? VerificationContext);
