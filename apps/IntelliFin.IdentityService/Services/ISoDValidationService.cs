using IntelliFin.IdentityService.Models;

namespace IntelliFin.IdentityService.Services;

public interface ISoDValidationService
{
    Task<SoDValidationResult> ValidateRoleAssignmentAsync(string userId, string roleId, CancellationToken ct = default);
    Task<SoDValidationResult> ValidatePermissionConflictsAsync(string userId, string[] newPermissions, CancellationToken ct = default);
    Task<SoDViolationReport> DetectViolationsAsync(CancellationToken ct = default);
}
