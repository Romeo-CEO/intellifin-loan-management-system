namespace IntelliFin.AdminService.Contracts.Responses;

public sealed record SodExceptionSummaryDto(
    Guid ExceptionId,
    string RequestedRole,
    DateTime ExpiresAt,
    string? Justification,
    string? ApprovedBy);
