namespace IntelliFin.IdentityService.Services;

public interface IAccountLockoutService
{
    Task<bool> IsAccountLockedAsync(string username, CancellationToken cancellationToken = default);
    Task<int> GetFailedAttemptsAsync(string username, CancellationToken cancellationToken = default);
    Task<DateTime?> GetLockoutEndAsync(string username, CancellationToken cancellationToken = default);
    Task RecordFailedAttemptAsync(string username, string ipAddress, CancellationToken cancellationToken = default);
    Task ResetFailedAttemptsAsync(string username, CancellationToken cancellationToken = default);
    Task<bool> UnlockAccountAsync(string username, CancellationToken cancellationToken = default);
    Task<TimeSpan?> GetRemainingLockoutTimeAsync(string username, CancellationToken cancellationToken = default);
}