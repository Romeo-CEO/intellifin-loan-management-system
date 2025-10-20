using StackExchange.Redis;
using System.Text.Json;

namespace IntelliFin.IdentityService.Services;

/// <summary>
/// Data stored for OIDC state parameter
/// </summary>
public class OidcStateData
{
    public string CodeVerifier { get; set; } = string.Empty;
    public string? ReturnUrl { get; set; }
    public string UserAgentHash { get; set; } = string.Empty;
    public string Nonce { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Store for OIDC state and PKCE code verifiers in Redis
/// Binds state to user agent for additional security
/// </summary>
public interface IOidcStateStore
{
    Task StoreAsync(string state, string codeVerifier, string nonce, string? returnUrl, string userAgentHash, CancellationToken cancellationToken = default);
    Task<OidcStateData?> GetAsync(string state, CancellationToken cancellationToken = default);
    Task RemoveAsync(string state, CancellationToken cancellationToken = default);
}

/// <summary>
/// Redis implementation of OIDC state store
/// </summary>
public class OidcStateStore : IOidcStateStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<OidcStateStore> _logger;
    private const string KeyPrefix = "oidc:state:";
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(10);

    public OidcStateStore(
        IConnectionMultiplexer redis,
        ILogger<OidcStateStore> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task StoreAsync(
        string state,
        string codeVerifier,
        string nonce,
        string? returnUrl,
        string userAgentHash,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(state))
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (string.IsNullOrEmpty(codeVerifier))
        {
            throw new ArgumentNullException(nameof(codeVerifier));
        }

        var data = new OidcStateData
        {
            CodeVerifier = codeVerifier,
            ReturnUrl = returnUrl,
            UserAgentHash = userAgentHash,
            Nonce = nonce,
            CreatedAt = DateTime.UtcNow
        };

        var key = GetKey(state);
        var json = JsonSerializer.Serialize(data);

        var db = _redis.GetDatabase();
        await db.StringSetAsync(key, json, DefaultExpiration);

        _logger.LogDebug(
            "Stored OIDC state {State} with {Expiration}s expiration",
            state,
            DefaultExpiration.TotalSeconds);
    }

    public async Task<OidcStateData?> GetAsync(string state, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(state))
        {
            return null;
        }

        var key = GetKey(state);
        var db = _redis.GetDatabase();
        
        var json = await db.StringGetAsync(key);
        if (!json.HasValue)
        {
            _logger.LogWarning("OIDC state {State} not found in Redis", state);
            return null;
        }

        try
        {
            var data = JsonSerializer.Deserialize<OidcStateData>(json.ToString());
            _logger.LogDebug("Retrieved OIDC state {State}", state);
            return data;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize OIDC state data for {State}", state);
            return null;
        }
    }

    public async Task RemoveAsync(string state, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(state))
        {
            return;
        }

        var key = GetKey(state);
        var db = _redis.GetDatabase();
        
        var deleted = await db.KeyDeleteAsync(key);
        
        if (deleted)
        {
            _logger.LogDebug("Removed OIDC state {State} from Redis", state);
        }
    }

    private static string GetKey(string state)
    {
        return $"{KeyPrefix}{state}";
    }
}
