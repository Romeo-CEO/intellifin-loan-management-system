namespace IntelliFin.AdminService.Contracts.Responses;

public sealed record SodValidationResponse(
    bool HasConflict,
    IReadOnlyCollection<SodConflictDto> Conflicts,
    bool CanProceedWithOverride);
