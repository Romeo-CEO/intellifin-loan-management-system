namespace IntelliFin.AdminService.Contracts.Responses;

public sealed record SBOMDto(
    string ServiceName,
    string Version,
    string ImageDigest,
    string Registry,
    DateTime BuildTimestamp,
    bool IsSigned,
    bool SignatureVerified,
    string? SignatureAuthor,
    DateTime? SignatureTimestamp,
    bool HasSbom,
    string? SbomPath,
    string? SbomFormat,
    IReadOnlyCollection<VulnerabilityDto> Vulnerabilities);
