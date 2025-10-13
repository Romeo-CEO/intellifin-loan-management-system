namespace IntelliFin.AdminService.Contracts.Responses;

public sealed record RoleAssignmentResult(
    Guid RoleAssignmentId,
    bool Success);
