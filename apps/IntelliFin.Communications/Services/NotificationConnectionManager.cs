using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using System.Collections.Concurrent;

namespace IntelliFin.Communications.Services;

public class NotificationConnectionManager : INotificationConnectionManager
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<NotificationConnectionManager> _logger;
    private readonly ConcurrentDictionary<string, HashSet<string>> _connections = new();
    private const string ConnectionCachePrefix = "user_connections:";
    private const int ConnectionCacheExpirationMinutes = 60;

    public NotificationConnectionManager(
        IDistributedCache cache,
        ILogger<NotificationConnectionManager> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task AddConnectionAsync(string userId, string connectionId)
    {
        try
        {
            // Add to in-memory cache
            _connections.AddOrUpdate(userId, 
                new HashSet<string> { connectionId },
                (key, existing) =>
                {
                    existing.Add(connectionId);
                    return existing;
                });

            // Add to distributed cache for scaling across instances
            var cacheKey = $"{ConnectionCachePrefix}{userId}";
            var connections = await GetConnectionsFromCacheAsync(userId);
            connections.Add(connectionId);
            
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(ConnectionCacheExpirationMinutes)
            };
            
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(connections.ToList()), cacheOptions);
            
            _logger.LogDebug("Added connection {ConnectionId} for user {UserId}", connectionId, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding connection {ConnectionId} for user {UserId}", connectionId, userId);
        }
    }

    public async Task RemoveConnectionAsync(string userId, string connectionId)
    {
        try
        {
            // Remove from in-memory cache
            if (_connections.TryGetValue(userId, out var userConnections))
            {
                userConnections.Remove(connectionId);
                if (!userConnections.Any())
                {
                    _connections.TryRemove(userId, out _);
                }
            }

            // Remove from distributed cache
            var cacheKey = $"{ConnectionCachePrefix}{userId}";
            var connections = await GetConnectionsFromCacheAsync(userId);
            connections.Remove(connectionId);
            
            if (connections.Any())
            {
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(ConnectionCacheExpirationMinutes)
                };
                await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(connections.ToList()), cacheOptions);
            }
            else
            {
                await _cache.RemoveAsync(cacheKey);
            }
            
            _logger.LogDebug("Removed connection {ConnectionId} for user {UserId}", connectionId, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing connection {ConnectionId} for user {UserId}", connectionId, userId);
        }
    }

    public async Task<IEnumerable<string>> GetConnectionsAsync(string userId)
    {
        try
        {
            // Try in-memory first
            if (_connections.TryGetValue(userId, out var memoryConnections))
            {
                return memoryConnections.ToList();
            }

            // Fallback to distributed cache
            var cacheConnections = await GetConnectionsFromCacheAsync(userId);
            
            // Update in-memory cache
            if (cacheConnections.Any())
            {
                _connections.TryAdd(userId, cacheConnections);
            }
            
            return cacheConnections.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting connections for user {UserId}", userId);
            return Enumerable.Empty<string>();
        }
    }

    public async Task<bool> IsUserOnlineAsync(string userId)
    {
        try
        {
            var connections = await GetConnectionsAsync(userId);
            return connections.Any();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if user {UserId} is online", userId);
            return false;
        }
    }

    public async Task<int> GetOnlineUsersCountAsync()
    {
        try
        {
            return _connections.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting online users count");
            return 0;
        }
    }

    public async Task<IEnumerable<string>> GetOnlineUsersAsync()
    {
        try
        {
            return _connections.Keys.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting online users");
            return Enumerable.Empty<string>();
        }
    }

    public async Task RemoveAllConnectionsAsync(string userId)
    {
        try
        {
            // Remove from in-memory cache
            _connections.TryRemove(userId, out _);

            // Remove from distributed cache
            var cacheKey = $"{ConnectionCachePrefix}{userId}";
            await _cache.RemoveAsync(cacheKey);
            
            _logger.LogDebug("Removed all connections for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing all connections for user {UserId}", userId);
        }
    }

    private async Task<HashSet<string>> GetConnectionsFromCacheAsync(string userId)
    {
        try
        {
            var cacheKey = $"{ConnectionCachePrefix}{userId}";
            var cachedData = await _cache.GetStringAsync(cacheKey);
            
            if (!string.IsNullOrEmpty(cachedData))
            {
                var connections = JsonSerializer.Deserialize<List<string>>(cachedData) ?? new List<string>();
                return new HashSet<string>(connections);
            }
            
            return new HashSet<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting connections from cache for user {UserId}", userId);
            return new HashSet<string>();
        }
    }
}