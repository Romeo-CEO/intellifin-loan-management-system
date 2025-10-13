using IntelliFin.IdentityService.Configuration;
using IntelliFin.IdentityService.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;

namespace IntelliFin.IdentityService.Services;

public class JwtTokenService : IJwtTokenService
{
    private readonly JwtConfiguration _jwtConfig;
    private readonly RedisConfiguration _redisConfig;
    private readonly IDatabase _redis;
    private readonly ILogger<JwtTokenService> _logger;
    private readonly ITokenFamilyService _tokenFamilyService;
    private readonly IAuditService _auditService;
    private readonly TokenValidationParameters _tokenValidationParameters;
    private readonly JwtSecurityTokenHandler _tokenHandler;

    public JwtTokenService(
        IOptions<JwtConfiguration> jwtConfig,
        IOptions<RedisConfiguration> redisConfig,
        IConnectionMultiplexer redis,
        ITokenFamilyService tokenFamilyService,
        IAuditService auditService,
        ILogger<JwtTokenService> logger)
    {
        _jwtConfig = jwtConfig.Value;
        _redisConfig = redisConfig.Value;
        _redis = redis.GetDatabase(_redisConfig.Database);
        _tokenFamilyService = tokenFamilyService;
        _auditService = auditService;
        _logger = logger;
        _tokenHandler = new JwtSecurityTokenHandler();

        _tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = _jwtConfig.ValidateIssuerSigningKey,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtConfig.SigningKey)),
            ValidateIssuer = _jwtConfig.ValidateIssuer,
            ValidIssuer = _jwtConfig.Issuer,
            ValidateAudience = _jwtConfig.ValidateAudience,
            ValidAudience = _jwtConfig.Audience,
            ValidateLifetime = _jwtConfig.ValidateLifetime,
            RequireExpirationTime = _jwtConfig.RequireExpirationTime,
            ClockSkew = TimeSpan.FromMinutes(_jwtConfig.ClockSkew)
        };
    }

    public async Task<string> GenerateAccessTokenAsync(UserClaims userClaims, CancellationToken cancellationToken = default)
    {
        try
        {
            var tokenId = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow;
            var expires = now.AddMinutes(_jwtConfig.AccessTokenMinutes);

            var claims = userClaims.ToClaims();
            claims.Add(new Claim(JwtRegisteredClaimNames.Jti, tokenId));
            claims.Add(new Claim(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64));

            var credentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtConfig.SigningKey)),
                SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _jwtConfig.Issuer,
                audience: _jwtConfig.Audience,
                claims: claims,
                notBefore: now,
                expires: expires,
                signingCredentials: credentials);

            var tokenString = _tokenHandler.WriteToken(token);

            // Store token metadata in Redis
            var tokenKey = $"{_redisConfig.KeyPrefix}token:{tokenId}";
            await _redis.HashSetAsync(tokenKey, new HashEntry[]
            {
                new("user_id", userClaims.UserId),
                new("username", userClaims.Username),
                new("session_id", userClaims.SessionId ?? string.Empty),
                new("device_id", userClaims.DeviceId ?? string.Empty),
                new("created_at", now.ToString("O")),
                new("expires_at", expires.ToString("O"))
            });

            await _redis.KeyExpireAsync(tokenKey, expires.Subtract(now));

            _logger.LogInformation("Access token generated for user {UserId} with token ID {TokenId}", 
                userClaims.UserId, tokenId);

            return tokenString;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate access token for user {UserId}", userClaims.UserId);
            throw;
        }
    }

    public async Task<RefreshTokenResult> GenerateRefreshTokenAsync(string userId, string deviceId, string? familyId = null, string? previousToken = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var refreshToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray()) +
                              Convert.ToBase64String(Guid.NewGuid().ToByteArray());

            var now = DateTime.UtcNow;
            var refreshTokenLifetime = TimeSpan.FromDays(_jwtConfig.RefreshTokenDays);
            if (refreshTokenLifetime <= TimeSpan.Zero)
            {
                refreshTokenLifetime = TimeSpan.FromDays(1);
            }

            var refreshTokenKey = $"{_redisConfig.KeyPrefix}refresh_token:{refreshToken}";

            var familyTtl = GetFamilyTtl();
            var registration = await _tokenFamilyService.RegisterTokenAsync(refreshToken, familyTtl, familyId, cancellationToken);

            await _redis.HashSetAsync(refreshTokenKey, new HashEntry[]
            {
                new("user_id", userId),
                new("device_id", deviceId),
                new("created_at", now.ToString("O")),
                new("expires_at", now.Add(refreshTokenLifetime).ToString("O")),
                new("is_active", "true"),
                new("family_id", registration.FamilyId),
                new("sequence", registration.Sequence.ToString()),
                new("previous_token", previousToken ?? string.Empty)
            });

            await _redis.KeyExpireAsync(refreshTokenKey, refreshTokenLifetime);

            _logger.LogInformation("Refresh token generated for user {UserId} on device {DeviceId} with family {FamilyId}", userId, deviceId, registration.FamilyId);

            return new RefreshTokenResult
            {
                Token = refreshToken,
                FamilyId = registration.FamilyId,
                ExpiresAt = now.Add(refreshTokenLifetime),
                Sequence = registration.Sequence
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate refresh token for user {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        try
        {
            var principal = _tokenHandler.ValidateToken(token, _tokenValidationParameters, out var validatedToken);
            
            if (validatedToken is not JwtSecurityToken jwtToken)
                return false;

            var tokenId = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;
            if (string.IsNullOrEmpty(tokenId))
                return false;

            return !await IsTokenRevokedAsync(tokenId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token validation failed");
            return false;
        }
    }

    public async Task<UserClaims?> GetClaimsFromTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        try
        {
            var principal = _tokenHandler.ValidateToken(token, _tokenValidationParameters, out var validatedToken);
            
            if (validatedToken is not JwtSecurityToken jwtToken)
                return null;

            var claims = principal.Claims.ToList();
            
            // Extract rule-based claims
            var standardClaimTypes = new HashSet<string>
            {
                ClaimTypes.NameIdentifier, ClaimTypes.Name, ClaimTypes.Email,
                ClaimTypes.GivenName, ClaimTypes.Surname, ClaimTypes.Role,
                "permission", "branch_id", "branch_name", "branch_region", "tenant_id", "session_id", "device_id",
                "auth_time", "auth_level", "ip_address", JwtRegisteredClaimNames.Jti,
                JwtRegisteredClaimNames.Iat, JwtRegisteredClaimNames.Exp,
                JwtRegisteredClaimNames.Nbf, JwtRegisteredClaimNames.Iss,
                JwtRegisteredClaimNames.Aud
            };

            var ruleClaims = claims
                .Where(c => !standardClaimTypes.Contains(c.Type))
                .ToDictionary(c => c.Type, c => c.Value);
            
            return new UserClaims
            {
                UserId = claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value ?? string.Empty,
                Username = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value ?? string.Empty,
                Email = claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value ?? string.Empty,
                FirstName = claims.FirstOrDefault(c => c.Type == ClaimTypes.GivenName)?.Value ?? string.Empty,
                LastName = claims.FirstOrDefault(c => c.Type == ClaimTypes.Surname)?.Value ?? string.Empty,
                Roles = claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToArray(),
                Permissions = claims.Where(c => c.Type == "permission").Select(c => c.Value).ToArray(),
                BranchId = claims.FirstOrDefault(c => c.Type == "branch_id")?.Value,
                BranchName = claims.FirstOrDefault(c => c.Type == "branch_name")?.Value,
                BranchRegion = claims.FirstOrDefault(c => c.Type == "branch_region")?.Value,
                TenantId = claims.FirstOrDefault(c => c.Type == "tenant_id")?.Value,
                SessionId = claims.FirstOrDefault(c => c.Type == "session_id")?.Value,
                DeviceId = claims.FirstOrDefault(c => c.Type == "device_id")?.Value,
                AuthenticationLevel = claims.FirstOrDefault(c => c.Type == "auth_level")?.Value ?? "basic",
                IpAddress = claims.FirstOrDefault(c => c.Type == "ip_address")?.Value,
                Rules = ruleClaims
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get claims from token");
            return null;
        }
    }

    public async Task<bool> IsTokenRevokedAsync(string tokenId, CancellationToken cancellationToken = default)
    {
        try
        {
            var revokedKey = $"{_redisConfig.KeyPrefix}revoked_token:{tokenId}";
            return await _redis.KeyExistsAsync(revokedKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if token {TokenId} is revoked", tokenId);
            return true; // Fail safe
        }
    }

    public async Task RevokeTokenAsync(string tokenId, CancellationToken cancellationToken = default)
    {
        try
        {
            var revokedKey = $"{_redisConfig.KeyPrefix}revoked_token:{tokenId}";
            await _redis.StringSetAsync(revokedKey, DateTime.UtcNow.ToString("O"), 
                TimeSpan.FromMinutes(_jwtConfig.AccessTokenMinutes + _jwtConfig.ClockSkew));

            _logger.LogInformation("Token {TokenId} has been revoked", tokenId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to revoke token {TokenId}", tokenId);
            throw;
        }
    }

    public async Task<bool> ValidateRefreshTokenAsync(string refreshToken, string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var refreshTokenKey = $"{_redisConfig.KeyPrefix}refresh_token:{refreshToken}";
            var tokenData = await _redis.HashGetAllAsync(refreshTokenKey);

            if (tokenData.Length == 0)
            {
                return false;
            }

            var tokenDict = tokenData.ToDictionary(h => h.Name.ToString(), h => h.Value.ToString());

            if (!tokenDict.TryGetValue("user_id", out var storedUserId) || storedUserId != userId)
            {
                return false;
            }

            if (!tokenDict.TryGetValue("is_active", out var isActive) || isActive != "true")
            {
                return false;
            }

            if (tokenDict.TryGetValue("expires_at", out var expiresAtStr) &&
                DateTime.TryParse(expiresAtStr, out var expiresAt) &&
                expiresAt <= DateTime.UtcNow)
            {
                return false;
            }

            if (tokenDict.TryGetValue("family_id", out var familyId) && !string.IsNullOrWhiteSpace(familyId))
            {
                if (await _tokenFamilyService.IsFamilyRevokedAsync(familyId, cancellationToken))
                {
                    return false;
                }

                if (!await _tokenFamilyService.IsTokenLatestAsync(familyId, refreshToken, cancellationToken))
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate refresh token for user {UserId}", userId);
            return false;
        }
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        try
        {
            var refreshTokenKey = $"{_redisConfig.KeyPrefix}refresh_token:{refreshToken}";
            await _redis.HashSetAsync(refreshTokenKey, new HashEntry[]
            {
                new("is_active", "false"),
                new("revoked_at", DateTime.UtcNow.ToString("O"))
            });

            _logger.LogInformation("Refresh token {RefreshToken} has been revoked", refreshToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to revoke refresh token");
            throw;
        }
    }

    public async Task<RefreshTokenRotationResult> RefreshTokensAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var refreshTokenKey = $"{_redisConfig.KeyPrefix}refresh_token:{request.RefreshToken}";
            var refreshTokenData = await _redis.HashGetAllAsync(refreshTokenKey);

            if (refreshTokenData.Length == 0)
            {
                throw new SecurityTokenException("Invalid refresh token");
            }

            var refreshTokenInfo = refreshTokenData.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());

            if (!refreshTokenInfo.TryGetValue("user_id", out var userId) || string.IsNullOrEmpty(userId))
            {
                throw new SecurityTokenException("Invalid refresh token data");
            }

            if (!refreshTokenInfo.TryGetValue("family_id", out var familyId) || string.IsNullOrWhiteSpace(familyId))
            {
                throw new SecurityTokenException("Refresh token family missing");
            }

            if (await _tokenFamilyService.IsFamilyRevokedAsync(familyId, cancellationToken))
            {
                var revokedTokens = await _tokenFamilyService.RevokeFamilyAsync(familyId, GetFamilyTtl(), cancellationToken);
                await LogReuseDetectionAsync(userId, familyId, request, revokedTokens, "family_revoked_attempt", cancellationToken);
                throw new SecurityTokenException("Refresh token family has been revoked");
            }

            if (!refreshTokenInfo.TryGetValue("is_active", out var isActive) || isActive != "true")
            {
                var revokedTokens = await _tokenFamilyService.RevokeFamilyAsync(familyId, GetFamilyTtl(), cancellationToken);
                await LogReuseDetectionAsync(userId, familyId, request, revokedTokens, "inactive_token_reuse", cancellationToken);
                throw new SecurityTokenException("Refresh token reuse detected");
            }

            if (refreshTokenInfo.TryGetValue("expires_at", out var expiresAtStr) &&
                DateTime.TryParse(expiresAtStr, out var expiresAt) &&
                expiresAt <= DateTime.UtcNow)
            {
                var revokedTokens = await _tokenFamilyService.RevokeFamilyAsync(familyId, GetFamilyTtl(), cancellationToken);
                await LogReuseDetectionAsync(userId, familyId, request, revokedTokens, "expired_token_reuse", cancellationToken);
                throw new SecurityTokenException("Refresh token has expired");
            }

            if (!await _tokenFamilyService.IsTokenLatestAsync(familyId, request.RefreshToken, cancellationToken))
            {
                var revokedTokens = await _tokenFamilyService.RevokeFamilyAsync(familyId, GetFamilyTtl(), cancellationToken);
                await LogReuseDetectionAsync(userId, familyId, request, revokedTokens, "stale_token_reuse", cancellationToken);
                throw new SecurityTokenException("Refresh token reuse detected");
            }

            var deviceId = refreshTokenInfo.TryGetValue("device_id", out var storedDeviceId)
                ? storedDeviceId
                : request.DeviceId ?? string.Empty;

            await _redis.HashSetAsync(refreshTokenKey, new HashEntry[]
            {
                new("is_active", "false"),
                new("rotated_at", DateTime.UtcNow.ToString("O"))
            });

            var newRefreshToken = await GenerateRefreshTokenAsync(userId, deviceId, familyId, request.RefreshToken, cancellationToken);

            var previousSequence = refreshTokenInfo.TryGetValue("sequence", out var sequenceValue) && long.TryParse(sequenceValue, out var sequenceNumber)
                ? sequenceNumber
                : 0;

            await _auditService.LogAsync(new AuditEvent
            {
                ActorId = userId,
                Action = "refresh_token.rotated",
                Entity = "refresh_token_family",
                EntityId = familyId,
                Timestamp = DateTime.UtcNow,
                IpAddress = request.IpAddress,
                UserAgent = request.UserAgent,
                Success = true,
                Result = "rotated",
                Details = new Dictionary<string, object>
                {
                    ["previousToken"] = request.RefreshToken,
                    ["previousTokenSequence"] = previousSequence,
                    ["newTokenSequence"] = newRefreshToken.Sequence,
                    ["deviceId"] = deviceId
                }
            }, cancellationToken);

            return new RefreshTokenRotationResult
            {
                UserId = userId,
                DeviceId = deviceId,
                FamilyId = newRefreshToken.FamilyId,
                RefreshToken = newRefreshToken.Token,
                RefreshTokenExpiresAt = newRefreshToken.ExpiresAt
            };
        }
        catch (SecurityTokenException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh tokens");
            throw;
        }
    }

    public async Task<TokenFamilyRevocationResult?> RevokeRefreshTokenFamilyAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        try
        {
            var refreshTokenKey = $"{_redisConfig.KeyPrefix}refresh_token:{refreshToken}";
            var refreshTokenData = await _redis.HashGetAllAsync(refreshTokenKey);

            if (refreshTokenData.Length == 0)
            {
                return null;
            }

            var refreshTokenInfo = refreshTokenData.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());

            if (!refreshTokenInfo.TryGetValue("family_id", out var familyId) || string.IsNullOrWhiteSpace(familyId))
            {
                return null;
            }

            var userId = refreshTokenInfo.TryGetValue("user_id", out var storedUserId) ? storedUserId : string.Empty;

            var revokedTokens = await _tokenFamilyService.RevokeFamilyAsync(familyId, GetFamilyTtl(), cancellationToken);

            await _auditService.LogAsync(new AuditEvent
            {
                ActorId = userId,
                Action = "refresh_token.family_revoked",
                Entity = "refresh_token_family",
                EntityId = familyId,
                Timestamp = DateTime.UtcNow,
                Success = true,
                Result = "revoked",
                Details = new Dictionary<string, object>
                {
                    ["initiatedBy"] = "api.auth.revoke",
                    ["revokedTokens"] = revokedTokens.Count
                }
            }, cancellationToken);

            return new TokenFamilyRevocationResult
            {
                FamilyId = familyId,
                UserId = userId,
                RevokedTokens = revokedTokens
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to revoke refresh token family for token {RefreshToken}", refreshToken);
            throw;
        }
    }

    private TimeSpan GetFamilyTtl()
    {
        var ttlDays = _redisConfig.TokenFamilyRetentionDays > 0
            ? _redisConfig.TokenFamilyRetentionDays
            : _redisConfig.RefreshTokenTimeoutDays;

        if (ttlDays <= 0)
        {
            ttlDays = 7;
        }

        return TimeSpan.FromDays(ttlDays);
    }

    private Task LogReuseDetectionAsync(string userId, string familyId, RefreshTokenRequest request, IReadOnlyList<string> revokedTokens, string reason, CancellationToken cancellationToken)
    {
        var details = new Dictionary<string, object>
        {
            ["revokedTokens"] = revokedTokens.Count,
            ["attemptedToken"] = request.RefreshToken,
            ["reason"] = reason
        };

        return _auditService.LogAsync(new AuditEvent
        {
            ActorId = userId,
            Action = "refresh_token.family_revoked",
            Entity = "refresh_token_family",
            EntityId = familyId,
            Timestamp = DateTime.UtcNow,
            IpAddress = request.IpAddress,
            UserAgent = request.UserAgent,
            Success = false,
            Severity = "Warning",
            Result = reason,
            Details = details
        }, cancellationToken);
    }
}