using IntelliFin.IdentityService.Configuration;
using IntelliFin.IdentityService.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Linq;

namespace IntelliFin.IdentityService.Services;

public class TokenFamilyService : ITokenFamilyService
{
    private readonly IDatabase _redis;
    private readonly RedisConfiguration _redisConfig;
    private readonly ILogger<TokenFamilyService> _logger;

    public TokenFamilyService(
        IOptions<RedisConfiguration> redisConfig,
        IConnectionMultiplexer connectionMultiplexer,
        ILogger<TokenFamilyService> logger)
    {
        _redisConfig = redisConfig.Value;
        _redis = connectionMultiplexer.GetDatabase(_redisConfig.Database);
        _logger = logger;
    }

    public async Task<TokenFamilyRegistration> RegisterTokenAsync(string refreshToken, TimeSpan familyTtl, string? familyId = null, CancellationToken cancellationToken = default)
    {
        var resolvedFamilyId = string.IsNullOrWhiteSpace(familyId) ? Guid.NewGuid().ToString("N") : familyId;

        if (await IsFamilyRevokedAsync(resolvedFamilyId, cancellationToken))
        {
            throw new InvalidOperationException($"Refresh token family {resolvedFamilyId} has been revoked");
        }

        var familyKey = $"{_redisConfig.KeyPrefix}token_family:{resolvedFamilyId}";
        var latestKey = $"{_redisConfig.KeyPrefix}token_family_latest:{resolvedFamilyId}";

        var sequence = await _redis.ListRightPushAsync(familyKey, refreshToken);
        if (familyTtl > TimeSpan.Zero)
        {
            await _redis.KeyExpireAsync(familyKey, familyTtl);
        }

        await _redis.StringSetAsync(latestKey, refreshToken, familyTtl > TimeSpan.Zero ? familyTtl : (TimeSpan?)null);

        _logger.LogDebug("Registered refresh token in family {FamilyId} with sequence {Sequence}", resolvedFamilyId, sequence - 1);

        return new TokenFamilyRegistration
        {
            FamilyId = resolvedFamilyId,
            Sequence = sequence - 1
        };
    }

    public async Task<bool> IsTokenLatestAsync(string familyId, string refreshToken, CancellationToken cancellationToken = default)
    {
        var latestKey = $"{_redisConfig.KeyPrefix}token_family_latest:{familyId}";
        var latestToken = await _redis.StringGetAsync(latestKey);
        if (!latestToken.IsNullOrEmpty)
        {
            return latestToken.ToString() == refreshToken;
        }

        var familyKey = $"{_redisConfig.KeyPrefix}token_family:{familyId}";
        var tokens = await _redis.ListRangeAsync(familyKey, -1, -1);
        if (tokens.Length == 0)
        {
            return false;
        }

        return tokens[0].ToString() == refreshToken;
    }

    public async Task<bool> IsFamilyRevokedAsync(string familyId, CancellationToken cancellationToken = default)
    {
        var revokedKey = $"{_redisConfig.KeyPrefix}token_family_revoked:{familyId}";
        var isRevoked = await _redis.StringGetAsync(revokedKey);
        return !isRevoked.IsNullOrEmpty && isRevoked.ToString().Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<string>> RevokeFamilyAsync(string familyId, TimeSpan familyTtl, CancellationToken cancellationToken = default)
    {
        var revokedKey = $"{_redisConfig.KeyPrefix}token_family_revoked:{familyId}";
        var familyKey = $"{_redisConfig.KeyPrefix}token_family:{familyId}";
        var latestKey = $"{_redisConfig.KeyPrefix}token_family_latest:{familyId}";

        var tokens = (await _redis.ListRangeAsync(familyKey, 0, -1)).Select(v => v.ToString()).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();

        await _redis.StringSetAsync(revokedKey, "true", familyTtl > TimeSpan.Zero ? familyTtl : (TimeSpan?)null);
        await _redis.KeyDeleteAsync(latestKey);

        if (familyTtl > TimeSpan.Zero)
        {
            await _redis.KeyExpireAsync(familyKey, familyTtl);
        }

        foreach (var token in tokens)
        {
            var tokenKey = $"{_redisConfig.KeyPrefix}refresh_token:{token}";
            await _redis.HashSetAsync(tokenKey, new HashEntry[]
            {
                new("is_active", "false"),
                new("revoked_at", DateTime.UtcNow.ToString("O"))
            });
        }

        _logger.LogInformation("Revoked refresh token family {FamilyId} containing {Count} tokens", familyId, tokens.Count);

        return tokens;
    }
}
