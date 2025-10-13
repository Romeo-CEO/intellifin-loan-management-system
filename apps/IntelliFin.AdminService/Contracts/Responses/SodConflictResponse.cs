namespace IntelliFin.AdminService.Contracts.Responses;

public sealed record SodConflictResponse(
    bool ConflictDetected,
    IReadOnlyCollection<string> ConflictingRoles,
    string ConflictDescription,
    string Severity,
    bool CanRequestException,
    string? ExceptionRequestUrl);
