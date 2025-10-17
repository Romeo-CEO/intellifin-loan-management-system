namespace IntelliFin.Contracts.Responses;

public sealed record RoleResponse(
    string Id,
    string Name,
    string? Description,
    bool Composite,
    bool ClientRole);