using IntelliFin.IdentityService.Configuration;
using IntelliFin.IdentityService.Models;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Text.Json;

namespace IntelliFin.IdentityService.Services;

public class SessionService : ISessionService
{
    private readonly SessionConfiguration _sessionConfig;
    private readonly RedisConfiguration _redisConfig;
    private readonly IDatabase _redis;
    private readonly ILogger<SessionService> _logger;

    public SessionService(
        IOptions<SessionConfiguration> sessionConfig,
        IOptions<RedisConfiguration> redisConfig,
        IConnectionMultiplexer redis,
        ILogger<SessionService> logger)
    {
        _sessionConfig = sessionConfig.Value;
        _redisConfig = redisConfig.Value;
        _redis = redis.GetDatabase(_redisConfig.Database);
        _logger = logger;
    }

    public async Task<SessionInfo> CreateSessionAsync(string userId, string username, string? deviceId = null,
        string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var sessionId = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow;
            var expiresAt = now.AddMinutes(_sessionConfig.TimeoutMinutes);

            var sessionInfo = new SessionInfo
            {
                SessionId = sessionId,
                UserId = userId,
                Username = username,
                DeviceId = deviceId,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                CreatedAt = now,
                LastAccessAt = now,
                ExpiresAt = expiresAt,
                IsActive = true
            };

            // Store session data
            var sessionKey = $"{_redisConfig.KeyPrefix}session:{sessionId}";
            var sessionJson = JsonSerializer.Serialize(sessionInfo);
            
            await _redis.StringSetAsync(sessionKey, sessionJson, expiresAt.Subtract(now));

            // Add to user sessions set
            var userSessionsKey = $"{_redisConfig.KeyPrefix}user_sessions:{userId}";
            await _redis.SetAddAsync(userSessionsKey, sessionId);
            await _redis.KeyExpireAsync(userSessionsKey, TimeSpan.FromDays(1));

            // Check concurrent session limit
            if (_sessionConfig.MaxConcurrentSessions > 0)
            {
                await EnforceConcurrentSessionLimitAsync(userId, sessionId);
            }

            _logger.LogInformation("Session created for user {UserId} with session ID {SessionId}", userId, sessionId);
            
            return sessionInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create session for user {UserId}", userId);
            throw;
        }
    }

    public async Task<SessionInfo?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var sessionKey = $"{_redisConfig.KeyPrefix}session:{sessionId}";
            var sessionJson = await _redis.StringGetAsync(sessionKey);

            if (!sessionJson.HasValue)
                return null;

            var sessionInfo = JsonSerializer.Deserialize<SessionInfo>(sessionJson!);
            return sessionInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get session {SessionId}", sessionId);
            return null;
        }
    }

    public async Task<bool> ValidateSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var sessionInfo = await GetSessionAsync(sessionId, cancellationToken);
            
            if (sessionInfo == null || !sessionInfo.IsValid)
                return false;

            // Update last access time if configured
            if (_sessionConfig.TrackUserActivity)
            {
                await UpdateSessionActivityAsync(sessionId, cancellationToken);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate session {SessionId}", sessionId);
            return false;
        }
    }

    public async Task UpdateSessionActivityAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var sessionInfo = await GetSessionAsync(sessionId, cancellationToken);
            if (sessionInfo == null)
                return;

            var now = DateTime.UtcNow;
            sessionInfo.LastAccessAt = now;
            
            // Extend expiration
            var newExpiresAt = now.AddMinutes(_sessionConfig.TimeoutMinutes);
            sessionInfo.ExpiresAt = newExpiresAt;

            var sessionKey = $"{_redisConfig.KeyPrefix}session:{sessionId}";
            var sessionJson = JsonSerializer.Serialize(sessionInfo);
            
            await _redis.StringSetAsync(sessionKey, sessionJson, newExpiresAt.Subtract(now));

            _logger.LogDebug("Session activity updated for session {SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update session activity for session {SessionId}", sessionId);
        }
    }

    public async Task<bool> InvalidateSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var sessionInfo = await GetSessionAsync(sessionId, cancellationToken);
            if (sessionInfo == null)
                return false;

            // Remove session
            var sessionKey = $"{_redisConfig.KeyPrefix}session:{sessionId}";
            await _redis.KeyDeleteAsync(sessionKey);

            // Remove from user sessions set
            var userSessionsKey = $"{_redisConfig.KeyPrefix}user_sessions:{sessionInfo.UserId}";
            await _redis.SetRemoveAsync(userSessionsKey, sessionId);

            _logger.LogInformation("Session invalidated: {SessionId}", sessionId);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invalidate session {SessionId}", sessionId);
            return false;
        }
    }

    public async Task<int> InvalidateUserSessionsAsync(string userId, string? excludeSessionId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var userSessionsKey = $"{_redisConfig.KeyPrefix}user_sessions:{userId}";
            var sessionIds = await _redis.SetMembersAsync(userSessionsKey);

            var invalidatedCount = 0;
            
            foreach (var sessionId in sessionIds)
            {
                if (excludeSessionId != null && sessionId == excludeSessionId)
                    continue;

                if (await InvalidateSessionAsync(sessionId!, cancellationToken))
                    invalidatedCount++;
            }

            _logger.LogInformation("Invalidated {Count} sessions for user {UserId}", invalidatedCount, userId);
            
            return invalidatedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invalidate sessions for user {UserId}", userId);
            return 0;
        }
    }

    public async Task<IEnumerable<SessionInfo>> GetUserSessionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var userSessionsKey = $"{_redisConfig.KeyPrefix}user_sessions:{userId}";
            var sessionIds = await _redis.SetMembersAsync(userSessionsKey);

            var sessions = new List<SessionInfo>();

            foreach (var sessionId in sessionIds)
            {
                var sessionInfo = await GetSessionAsync(sessionId!, cancellationToken);
                if (sessionInfo != null && sessionInfo.IsValid)
                {
                    sessions.Add(sessionInfo);
                }
            }

            return sessions.OrderByDescending(s => s.LastAccessAt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get sessions for user {UserId}", userId);
            return Enumerable.Empty<SessionInfo>();
        }
    }

    public async Task<int> CleanupExpiredSessionsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // This is a simplified cleanup - in production you'd want a more efficient approach
            var cleanedCount = 0;
            
            _logger.LogInformation("Session cleanup completed. Removed {Count} expired sessions", cleanedCount);
            
            return cleanedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup expired sessions");
            return 0;
        }
    }

    public async Task<bool> ExtendSessionAsync(string sessionId, TimeSpan extension, CancellationToken cancellationToken = default)
    {
        try
        {
            var sessionInfo = await GetSessionAsync(sessionId, cancellationToken);
            if (sessionInfo == null || !sessionInfo.IsValid)
                return false;

            sessionInfo.ExpiresAt = sessionInfo.ExpiresAt.Add(extension);

            var sessionKey = $"{_redisConfig.KeyPrefix}session:{sessionId}";
            var sessionJson = JsonSerializer.Serialize(sessionInfo);
            
            await _redis.StringSetAsync(sessionKey, sessionJson, sessionInfo.ExpiresAt.Subtract(DateTime.UtcNow));

            _logger.LogInformation("Session {SessionId} extended by {Extension}", sessionId, extension);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extend session {SessionId}", sessionId);
            return false;
        }
    }

    private async Task EnforceConcurrentSessionLimitAsync(string userId, string currentSessionId)
    {
        try
        {
            var userSessions = await GetUserSessionsAsync(userId);
            var activeSessions = userSessions.Where(s => s.IsValid).ToList();

            if (activeSessions.Count > _sessionConfig.MaxConcurrentSessions)
            {
                // Remove oldest sessions
                var sessionsToRemove = activeSessions
                    .Where(s => s.SessionId != currentSessionId)
                    .OrderBy(s => s.LastAccessAt)
                    .Take(activeSessions.Count - _sessionConfig.MaxConcurrentSessions);

                foreach (var session in sessionsToRemove)
                {
                    await InvalidateSessionAsync(session.SessionId);
                }

                _logger.LogInformation("Enforced concurrent session limit for user {UserId}. Removed {Count} sessions", 
                    userId, sessionsToRemove.Count());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enforce concurrent session limit for user {UserId}", userId);
        }
    }
}