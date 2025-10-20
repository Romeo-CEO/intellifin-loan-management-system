using IntelliFin.IdentityService.Models;

namespace IntelliFin.IdentityService.Services;

public interface ISessionService
{
    Task<SessionInfo> CreateSessionAsync(string userId, string username, string? deviceId = null, 
        string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default);
    
    Task<SessionInfo?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default);
    
    Task<bool> ValidateSessionAsync(string sessionId, CancellationToken cancellationToken = default);
    
    Task UpdateSessionActivityAsync(string sessionId, CancellationToken cancellationToken = default);
    
    Task<bool> InvalidateSessionAsync(string sessionId, CancellationToken cancellationToken = default);
    
    Task<int> InvalidateUserSessionsAsync(string userId, string? excludeSessionId = null, CancellationToken cancellationToken = default);
    
    Task<IEnumerable<SessionInfo>> GetUserSessionsAsync(string userId, CancellationToken cancellationToken = default);
    
    Task<int> CleanupExpiredSessionsAsync(CancellationToken cancellationToken = default);
    
    Task<bool> ExtendSessionAsync(string sessionId, TimeSpan extension, CancellationToken cancellationToken = default);

    Task<int> RevokeAllSessionsAsync(string userId, CancellationToken cancellationToken = default);
}
