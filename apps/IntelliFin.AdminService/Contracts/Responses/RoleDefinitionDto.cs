namespace IntelliFin.AdminService.Contracts.Responses;

public sealed record RoleDefinitionDto(
    string RoleName,
    string DisplayName,
    string? Description,
    string? Category,
    string? RiskLevel,
    bool RequiresApproval);
