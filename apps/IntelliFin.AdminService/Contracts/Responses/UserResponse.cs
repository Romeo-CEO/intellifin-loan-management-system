namespace IntelliFin.AdminService.Contracts.Responses;

public sealed record UserResponse(
    string Id,
    string Username,
    string? Email,
    string? FirstName,
    string? LastName,
    bool Enabled,
    bool EmailVerified,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Attributes);
