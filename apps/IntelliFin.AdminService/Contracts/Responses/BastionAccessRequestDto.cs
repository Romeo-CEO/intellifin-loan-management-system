namespace IntelliFin.AdminService.Contracts.Responses;

public record BastionAccessRequestDto(
    Guid RequestId,
    string Status,
    bool RequiresApproval,
    DateTime RequestedAt,
    DateTime? ApprovedAt,
    DateTime? ExpiresAt,
    string Environment,
    int AccessDurationHours,
    IReadOnlyCollection<string> TargetHosts,
    string? CertificateContent,
    string? BastionHost,
    string? ConnectionInstructions);
