using IntelliFin.IdentityService.Configuration;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace IntelliFin.IdentityService.Services;

public class AccountLockoutService : IAccountLockoutService
{
    private readonly AccountLockoutConfiguration _config;
    private readonly RedisConfiguration _redisConfig;
    private readonly IDatabase _redis;
    private readonly ILogger<AccountLockoutService> _logger;

    public AccountLockoutService(
        IOptions<AccountLockoutConfiguration> config,
        IOptions<RedisConfiguration> redisConfig,
        IConnectionMultiplexer redis,
        ILogger<AccountLockoutService> logger)
    {
        _config = config.Value;
        _redisConfig = redisConfig.Value;
        _redis = redis.GetDatabase(_redisConfig.Database);
        _logger = logger;
    }

    public async Task<bool> IsAccountLockedAsync(string username, CancellationToken cancellationToken = default)
    {
        if (!_config.EnableLockout)
            return false;

        try
        {
            var lockoutKey = $"{_redisConfig.KeyPrefix}lockout:{username}";
            var lockoutData = await _redis.HashGetAllAsync(lockoutKey);

            if (lockoutData.Length == 0)
                return false;

            var lockoutDict = lockoutData.ToDictionary(h => h.Name.ToString(), h => h.Value.ToString());

            if (!lockoutDict.TryGetValue("locked_until", out var lockedUntilStr) ||
                !DateTime.TryParse(lockedUntilStr, out var lockedUntil))
                return false;

            var isLocked = DateTime.UtcNow < lockedUntil;
            
            if (!isLocked)
            {
                // Cleanup expired lockout
                await _redis.KeyDeleteAsync(lockoutKey);
            }

            return isLocked;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check lockout status for user {Username}", username);
            return false;
        }
    }

    public async Task<int> GetFailedAttemptsAsync(string username, CancellationToken cancellationToken = default)
    {
        try
        {
            var attemptsKey = $"{_redisConfig.KeyPrefix}failed_attempts:{username}";
            var attempts = await _redis.StringGetAsync(attemptsKey);
            
            return attempts.HasValue ? (int)attempts : 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get failed attempts for user {Username}", username);
            return 0;
        }
    }

    public async Task<DateTime?> GetLockoutEndAsync(string username, CancellationToken cancellationToken = default)
    {
        try
        {
            var lockoutKey = $"{_redisConfig.KeyPrefix}lockout:{username}";
            var lockedUntilStr = await _redis.HashGetAsync(lockoutKey, "locked_until");

            if (!lockedUntilStr.HasValue)
                return null;

            return DateTime.TryParse(lockedUntilStr, out var lockedUntil) ? lockedUntil : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get lockout end time for user {Username}", username);
            return null;
        }
    }

    public async Task RecordFailedAttemptAsync(string username, string ipAddress, CancellationToken cancellationToken = default)
    {
        if (!_config.EnableLockout)
            return;

        try
        {
            var attemptsKey = $"{_redisConfig.KeyPrefix}failed_attempts:{username}";
            var currentAttempts = await GetFailedAttemptsAsync(username, cancellationToken);
            var newAttempts = currentAttempts + 1;

            // Record the failed attempt
            await _redis.StringSetAsync(attemptsKey, newAttempts, TimeSpan.FromMinutes(_config.AttemptsWindowMinutes));

            _logger.LogWarning("Failed login attempt {Attempt} recorded for user {Username} from IP {IpAddress}", 
                newAttempts, username, ipAddress);

            // Check if we should lock the account
            if (newAttempts >= _config.MaxFailedAttempts)
            {
                await LockAccountAsync(username, newAttempts);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record failed attempt for user {Username}", username);
        }
    }

    public async Task ResetFailedAttemptsAsync(string username, CancellationToken cancellationToken = default)
    {
        try
        {
            var attemptsKey = $"{_redisConfig.KeyPrefix}failed_attempts:{username}";
            await _redis.KeyDeleteAsync(attemptsKey);

            _logger.LogInformation("Failed attempts reset for user {Username}", username);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset failed attempts for user {Username}", username);
        }
    }

    public async Task<bool> UnlockAccountAsync(string username, CancellationToken cancellationToken = default)
    {
        try
        {
            var lockoutKey = $"{_redisConfig.KeyPrefix}lockout:{username}";
            var attemptsKey = $"{_redisConfig.KeyPrefix}failed_attempts:{username}";

            await Task.WhenAll(
                _redis.KeyDeleteAsync(lockoutKey),
                _redis.KeyDeleteAsync(attemptsKey)
            );

            _logger.LogInformation("Account unlocked for user {Username}", username);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unlock account for user {Username}", username);
            return false;
        }
    }

    public async Task<TimeSpan?> GetRemainingLockoutTimeAsync(string username, CancellationToken cancellationToken = default)
    {
        var lockoutEnd = await GetLockoutEndAsync(username, cancellationToken);
        
        if (lockoutEnd == null)
            return null;

        var remaining = lockoutEnd.Value - DateTime.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : null;
    }

    private async Task LockAccountAsync(string username, int attemptCount)
    {
        try
        {
            // Calculate lockout duration (progressive lockout)
            var lockoutDuration = CalculateLockoutDuration(attemptCount);
            var lockoutEnd = DateTime.UtcNow.AddMinutes(lockoutDuration);

            var lockoutKey = $"{_redisConfig.KeyPrefix}lockout:{username}";
            await _redis.HashSetAsync(lockoutKey, new HashEntry[]
            {
                new("locked_until", lockoutEnd.ToString("O")),
                new("locked_at", DateTime.UtcNow.ToString("O")),
                new("attempt_count", attemptCount.ToString()),
                new("duration_minutes", lockoutDuration.ToString())
            });

            await _redis.KeyExpireAsync(lockoutKey, TimeSpan.FromMinutes(lockoutDuration + 5));

            _logger.LogWarning("Account locked for user {Username} until {LockoutEnd} due to {AttemptCount} failed attempts", 
                username, lockoutEnd, attemptCount);

            // TODO: Send notification if configured
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to lock account for user {Username}", username);
        }
    }

    private int CalculateLockoutDuration(int attemptCount)
    {
        if (_config.ProgressiveLockoutDurations.Length == 0)
            return _config.LockoutDurationMinutes;

        var index = Math.Min(attemptCount - _config.MaxFailedAttempts, 
            _config.ProgressiveLockoutDurations.Length - 1);

        return _config.ProgressiveLockoutDurations[index];
    }
}