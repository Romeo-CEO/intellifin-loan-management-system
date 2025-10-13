namespace IntelliFin.AdminService.Contracts.Responses;

public sealed record UserRoleDto(
    string RoleName,
    string DisplayName,
    string? Category,
    string? RiskLevel,
    DateTime? AssignedAt);
