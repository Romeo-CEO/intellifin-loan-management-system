namespace IntelliFin.AdminService.Contracts.Responses;

public sealed record SodPolicyDto(
    int Id,
    string Role1,
    string Role2,
    string ConflictDescription,
    string Severity,
    bool Enabled);
