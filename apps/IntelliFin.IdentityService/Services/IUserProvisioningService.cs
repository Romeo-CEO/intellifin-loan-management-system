namespace IntelliFin.IdentityService.Services;

public interface IUserProvisioningService
{
    Task<ProvisioningResult> ProvisionUserAsync(string userId, CancellationToken cancellationToken = default);
    Task<ProvisioningResult> ProvisionUserByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<BulkProvisioningResult> ProvisionAllUsersAsync(CancellationToken cancellationToken = default);
    Task<bool> IsUserProvisionedAsync(string userId, CancellationToken cancellationToken = default);
}
