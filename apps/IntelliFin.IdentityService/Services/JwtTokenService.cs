using IntelliFin.IdentityService.Configuration;
using IntelliFin.IdentityService.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace IntelliFin.IdentityService.Services;

public class JwtTokenService : IJwtTokenService
{
    private readonly JwtConfiguration _jwtConfig;
    private readonly RedisConfiguration _redisConfig;
    private readonly IDatabase _redis;
    private readonly ILogger<JwtTokenService> _logger;
    private readonly TokenValidationParameters _tokenValidationParameters;
    private readonly JwtSecurityTokenHandler _tokenHandler;

    public JwtTokenService(
        IOptions<JwtConfiguration> jwtConfig,
        IOptions<RedisConfiguration> redisConfig,
        IConnectionMultiplexer redis,
        ILogger<JwtTokenService> logger)
    {
        _jwtConfig = jwtConfig.Value;
        _redisConfig = redisConfig.Value;
        _redis = redis.GetDatabase(_redisConfig.Database);
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

    public async Task<string> GenerateRefreshTokenAsync(string userId, string deviceId, CancellationToken cancellationToken = default)
    {
        try
        {
            var refreshToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray()) + 
                              Convert.ToBase64String(Guid.NewGuid().ToByteArray());
            
            var now = DateTime.UtcNow;
            var expires = now.AddDays(_jwtConfig.RefreshTokenDays);

            var refreshTokenKey = $"{_redisConfig.KeyPrefix}refresh_token:{refreshToken}";
            await _redis.HashSetAsync(refreshTokenKey, new HashEntry[]
            {
                new("user_id", userId),
                new("device_id", deviceId),
                new("created_at", now.ToString("O")),
                new("expires_at", expires.ToString("O")),
                new("is_active", "true")
            });

            await _redis.KeyExpireAsync(refreshTokenKey, expires.Subtract(now));

            _logger.LogInformation("Refresh token generated for user {UserId} and device {DeviceId}", userId, deviceId);

            return refreshToken;
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
                SessionId = claims.FirstOrDefault(c => c.Type == "session_id")?.Value,
                DeviceId = claims.FirstOrDefault(c => c.Type == "device_id")?.Value,
                AuthenticationLevel = claims.FirstOrDefault(c => c.Type == "auth_level")?.Value ?? "basic",
                IpAddress = claims.FirstOrDefault(c => c.Type == "ip_address")?.Value
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
                return false;

            var tokenDict = tokenData.ToDictionary(h => h.Name.ToString(), h => h.Value.ToString());
            
            if (!tokenDict.TryGetValue("user_id", out var storedUserId) || storedUserId != userId)
                return false;

            if (!tokenDict.TryGetValue("is_active", out var isActive) || isActive != "true")
                return false;

            if (tokenDict.TryGetValue("expires_at", out var expiresAtStr) && 
                DateTime.TryParse(expiresAtStr, out var expiresAt) && 
                expiresAt <= DateTime.UtcNow)
                return false;

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
            await _redis.HashSetAsync(refreshTokenKey, "is_active", "false");

            _logger.LogInformation("Refresh token has been revoked");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to revoke refresh token");
            throw;
        }
    }

    public async Task<TokenResponse> RefreshTokensAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate refresh token
            var refreshTokenKey = $"{_redisConfig.KeyPrefix}refresh_token:{request.RefreshToken}";
            var refreshTokenData = await _redis.HashGetAllAsync(refreshTokenKey);

            if (refreshTokenData.Length == 0)
            {
                throw new SecurityTokenException("Invalid refresh token");
            }

            var refreshTokenInfo = refreshTokenData.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());

            if (!refreshTokenInfo.TryGetValue("is_active", out var isActive) || isActive != "true")
            {
                throw new SecurityTokenException("Refresh token is not active");
            }

            if (!refreshTokenInfo.TryGetValue("expires_at", out var expiresAtStr) ||
                !DateTime.TryParse(expiresAtStr, out var expiresAt) ||
                expiresAt <= DateTime.UtcNow)
            {
                throw new SecurityTokenException("Refresh token has expired");
            }

            if (!refreshTokenInfo.TryGetValue("user_id", out var userId))
            {
                throw new SecurityTokenException("Invalid refresh token data");
            }

            // Revoke the old refresh token
            await _redis.HashSetAsync(refreshTokenKey, "is_active", "false");

            // Return the user ID for the caller to generate new tokens
            return new TokenResponse
            {
                AccessToken = string.Empty, // Will be set by the caller
                RefreshToken = string.Empty, // Will be set by the caller
                ExpiresIn = _jwtConfig.AccessTokenMinutes * 60,
                IssuedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtConfig.AccessTokenMinutes),
                User = new UserInfo { Id = userId } // Minimal user info, caller should populate
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh tokens");
            throw;
        }
    }
}