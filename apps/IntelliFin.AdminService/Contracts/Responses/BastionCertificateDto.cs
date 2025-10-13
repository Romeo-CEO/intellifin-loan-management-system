namespace IntelliFin.AdminService.Contracts.Responses;

public record BastionCertificateDto(
    Guid RequestId,
    string CertificateContent,
    DateTime ExpiresAt,
    string? BastionHost);
