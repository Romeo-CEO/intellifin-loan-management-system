namespace IntelliFin.AdminService.Contracts.Responses;

public sealed record SodExceptionStatusDto(
    Guid ExceptionId,
    string Status,
    string RequestedRole,
    IReadOnlyCollection<string> ConflictingRoles,
    string BusinessJustification,
    DateTime RequestedAt,
    string RequestedBy,
    DateTime? ApprovedAt,
    DateTime? ExpiresAt,
    string? ReviewedBy,
    string? ReviewComments);
