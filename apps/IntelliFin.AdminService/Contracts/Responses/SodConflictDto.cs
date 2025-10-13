namespace IntelliFin.AdminService.Contracts.Responses;

public sealed record SodConflictDto(
    int PolicyId,
    string ConflictingRole,
    string ConflictDescription,
    string Severity);
