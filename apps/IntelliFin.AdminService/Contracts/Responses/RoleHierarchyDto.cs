namespace IntelliFin.AdminService.Contracts.Responses;

public sealed record RoleHierarchyDto(
    string ParentRole,
    string ChildRole);
