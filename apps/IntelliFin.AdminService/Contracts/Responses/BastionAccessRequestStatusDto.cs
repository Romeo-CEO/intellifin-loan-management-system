namespace IntelliFin.AdminService.Contracts.Responses;

public record BastionAccessRequestStatusDto(
    Guid RequestId,
    string Status,
    bool RequiresApproval,
    DateTime RequestedAt,
    DateTime? ApprovedAt,
    DateTime? ExpiresAt,
    string Environment);
