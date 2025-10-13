using IntelliFin.AdminService.Contracts.Responses;

namespace IntelliFin.AdminService.Services;

public interface IVaultManagementService
{
    Task<VaultLeaseRevocationResult> RevokeLeaseAsync(
        string leaseId,
        string? reason,
        string? incidentId,
        string requestedBy,
        string? requestedByName,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<VaultLeaseDto>> GetActiveLeasesAsync(
        string? serviceName,
        CancellationToken cancellationToken);
}
