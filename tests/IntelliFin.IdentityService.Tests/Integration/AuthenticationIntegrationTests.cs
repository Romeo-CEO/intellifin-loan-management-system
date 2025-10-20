using IntelliFin.IdentityService.Models;
using IntelliFin.IdentityService.Services;
using IntelliFin.IdentityService.Configuration;
using IntelliFin.Shared.DomainModels.Data;
using IntelliFin.Shared.DomainModels.Entities;
using IntelliFin.Shared.DomainModels.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Xunit;

namespace IntelliFin.IdentityService.Tests.Integration;

public class AuthenticationIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public AuthenticationIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("UseInMemoryDatabase", "true");
            builder.UseSetting("InMemoryDatabaseName", "IdentityServiceTests");
            builder.UseSetting("SeedBaselineData", "false");
            builder.ConfigureServices(services =>
            {
                // Remove hosted services (e.g., Vault watchers) to avoid external deps
                var hostedServices = services.Where(d => d.ServiceType == typeof(IHostedService)).ToList();
                foreach (var hs in hostedServices)
                {
                    services.Remove(hs);
                }

                // Remove Redis connection multiplexer if registered
                var redisDescriptor = services.SingleOrDefault(d => d.ServiceType.FullName == "StackExchange.Redis.IConnectionMultiplexer");
                if (redisDescriptor != null) services.Remove(redisDescriptor);

                // Replace critical services with in-memory test doubles
                ReplaceService<IJwtTokenService, TestJwtTokenService>(services);
                ReplaceService<ISessionService, TestSessionService>(services);
                ReplaceService<IAccountLockoutService, TestAccountLockoutService>(services);

                // Replace Vault credential service with a test stub
                services.AddSingleton<IVaultDatabaseCredentialService>(new FakeVaultDbCreds());

                // Seed a test user into the in-memory database
                using var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var ctx = scope.ServiceProvider.GetRequiredService<LmsDbContext>();
                var repo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
                var passwordSvc = scope.ServiceProvider.GetRequiredService<IPasswordService>();

                if (!ctx.Users.Any(u => u.Username == "testuser"))
                {
                    var hash = passwordSvc.HashPasswordAsync("Password123!").GetAwaiter().GetResult();
                    var user = new User
                    {
                        Username = "testuser",
                        Email = "testuser@example.com",
                        FirstName = "Test",
                        LastName = "User",
                        PasswordHash = hash,
                        IsActive = true,
                        CreatedBy = "tests"
                    };
                    repo.CreateAsync(user).GetAwaiter().GetResult();
                }
            });
        });
        
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsToken()
    {
        // Arrange
        var loginRequest = new LoginRequest
        {
            Username = "testuser",
            Password = "Password123!"
        };

        var json = JsonSerializer.Serialize(loginRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/auth/login", content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseContent, new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true 
        });

        Assert.NotNull(tokenResponse);
        Assert.NotEmpty(tokenResponse.AccessToken);
        Assert.NotEmpty(tokenResponse.RefreshToken);
        Assert.Equal("Bearer", tokenResponse.TokenType);
        Assert.True(tokenResponse.ExpiresIn > 0);
    }

    [Fact]
    public async Task Login_InvalidCredentials_ReturnsUnauthorized()
    {
        // Arrange
        var loginRequest = new LoginRequest
        {
            Username = "testuser",
            Password = "wrongpassword"
        };

        var json = JsonSerializer.Serialize(loginRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/auth/login", content);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_MissingCredentials_ReturnsBadRequest()
    {
        // Arrange
        var loginRequest = new LoginRequest
        {
            Username = "",
            Password = "Password123!"
        };

        var json = JsonSerializer.Serialize(loginRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/auth/login", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetCurrentUser_WithValidToken_ReturnsUserInfo()
    {
        // Arrange
        var token = await GetValidTokenAsync();
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/auth/me");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        var userInfo = JsonSerializer.Deserialize<UserInfo>(responseContent, new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true 
        });

        Assert.NotNull(userInfo);
        Assert.NotEmpty(userInfo.Username);
        Assert.True(userInfo.IsActive);
    }

    [Fact]
    public async Task GetCurrentUser_WithoutToken_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/auth/me");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_WithValidToken_ReturnsOk()
    {
        // Arrange
        var token = await GetValidTokenAsync();
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PostAsync("/api/auth/logout", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ValidateToken_WithValidToken_ReturnsValid()
    {
        // Arrange
        var token = await GetValidTokenAsync();
        var json = JsonSerializer.Serialize(token);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/auth/validate-token", content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        var validationResult = JsonSerializer.Deserialize<JsonElement>(responseContent);
        
        Assert.True(validationResult.GetProperty("isValid").GetBoolean());
        Assert.True(validationResult.TryGetProperty("claims", out var claimsElement));
        Assert.True(claimsElement.TryGetProperty("userId", out var _));
        Assert.True(claimsElement.TryGetProperty("username", out var _));
    }

    [Fact]
    public async Task ValidateToken_WithInvalidToken_ReturnsInvalid()
    {
        // Arrange
        const string invalidToken = "invalid.token.here";
        var json = JsonSerializer.Serialize(invalidToken);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/auth/validate-token", content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        var validationResult = JsonSerializer.Deserialize<JsonElement>(responseContent);
        
        Assert.False(validationResult.GetProperty("isValid").GetBoolean());
    }

    [Fact]
    public async Task AccountLockout_MultipleFailedAttempts_LocksAccount()
    {
        // Arrange
        var loginRequest = new LoginRequest
        {
            Username = "lockouttest",
            Password = "wrongpassword"
        };

        var json = JsonSerializer.Serialize(loginRequest);

        // Act - Make multiple failed login attempts
        for (int i = 0; i < 6; i++) // Exceed the 5 attempt limit
        {
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _client.PostAsync("/api/auth/login", content);
            
            if (i < 5)
            {
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            }
            else
            {
                // The 6th attempt should return 423 Locked
                Assert.Equal(HttpStatusCode.Locked, response.StatusCode);
            }
        }
    }

    [Fact]
    public async Task HealthCheck_ReturnsHealthy()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RootEndpoint_ReturnsServiceInfo()
    {
        // Act
        var response = await _client.GetAsync("/");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        var serviceInfo = JsonSerializer.Deserialize<JsonElement>(responseContent);
        
        Assert.Equal("IntelliFin.IdentityService", serviceInfo.GetProperty("name").GetString());
        Assert.Equal("OK", serviceInfo.GetProperty("status").GetString());
    }

    private async Task<string> GetValidTokenAsync()
    {
        var loginRequest = new LoginRequest
        {
            Username = "testuser",
            Password = "Password123!"
        };

        var json = JsonSerializer.Serialize(loginRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/auth/login", content);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync();
        var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseContent, new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true 
        });

        return tokenResponse?.AccessToken ?? throw new InvalidOperationException("Failed to get valid token");
    }
    
    private class FakeVaultDbCreds : IVaultDatabaseCredentialService
    {
        public event EventHandler<DatabaseCredential>? CredentialsRotated;
        public DatabaseCredential GetCurrentCredentials() => new DatabaseCredential
        {
            Username = "testuser",
            Password = "testpass",
            LeaseId = "test",
            LeaseDuration = 3600,
            Renewable = false,
            LoadedAt = DateTime.UtcNow
        };
    }

    private static void ReplaceService<TService, TImplementation>(IServiceCollection services)
        where TService : class
        where TImplementation : class, TService
    {
        var descriptors = services.Where(d => d.ServiceType == typeof(TService)).ToList();
        foreach (var d in descriptors) services.Remove(d);
        services.AddSingleton<TService, TImplementation>();
    }

    private class TestJwtTokenService : IJwtTokenService
    {
        private readonly JwtConfiguration _jwtConfig;
        private readonly ILogger<TestJwtTokenService> _logger;
        private readonly JwtSecurityTokenHandler _handler = new();
        private readonly TokenValidationParameters _validationParams;
        private readonly ConcurrentDictionary<string, DateTime> _revoked = new();
        private readonly ConcurrentDictionary<string, (string UserId, string DeviceId, DateTime ExpiresAt, string FamilyId, long Sequence, bool Active)> _refresh = new();

        public TestJwtTokenService(IOptions<JwtConfiguration> jwtConfig, ILogger<TestJwtTokenService> logger)
        {
            _jwtConfig = jwtConfig.Value;
            _logger = logger;
            _validationParams = new TokenValidationParameters
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

        public Task<string> GenerateAccessTokenAsync(UserClaims userClaims, CancellationToken cancellationToken = default)
        {
            var tokenId = Guid.NewGuid().ToString("N");
            var now = DateTime.UtcNow;
            var expires = now.AddMinutes(_jwtConfig.AccessTokenMinutes);

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, userClaims.UserId),
                new(ClaimTypes.NameIdentifier, userClaims.UserId),
                new("name", userClaims.Username),
                new(ClaimTypes.Name, userClaims.Username),
                new("email", userClaims.Email),
                new(ClaimTypes.Email, userClaims.Email),
                new("given_name", userClaims.FirstName),
                new(ClaimTypes.GivenName, userClaims.FirstName),
                new("family_name", userClaims.LastName),
                new(ClaimTypes.Surname, userClaims.LastName),
                new("session_id", userClaims.SessionId ?? string.Empty),
                new("device_id", userClaims.DeviceId ?? string.Empty),
                new("auth_time", now.ToString("O")),
                new("auth_level", userClaims.AuthenticationLevel),
                new("ip_address", userClaims.IpAddress ?? string.Empty),
                new(JwtRegisteredClaimNames.Jti, tokenId),
                new(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
            };

            if (!string.IsNullOrEmpty(userClaims.BranchId)) claims.Add(new Claim("branch_id", userClaims.BranchId!));
            if (!string.IsNullOrEmpty(userClaims.BranchName)) claims.Add(new Claim("branch_name", userClaims.BranchName!));
            if (!string.IsNullOrEmpty(userClaims.BranchRegion)) claims.Add(new Claim("branch_region", userClaims.BranchRegion!));
            if (!string.IsNullOrEmpty(userClaims.TenantId)) claims.Add(new Claim("tenant_id", userClaims.TenantId!));

            foreach (var role in userClaims.Roles)
            {
                claims.Add(new Claim("role", role));
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
            foreach (var perm in userClaims.Permissions)
            {
                claims.Add(new Claim("permission", perm));
            }

            var creds = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtConfig.SigningKey)),
                SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _jwtConfig.Issuer,
                audience: _jwtConfig.Audience,
                claims: claims,
                notBefore: now,
                expires: expires,
                signingCredentials: creds);

            var tokenString = _handler.WriteToken(token);
            return Task.FromResult(tokenString);
        }

        public Task<RefreshTokenResult> GenerateRefreshTokenAsync(string userId, string deviceId, string? familyId = null, string? previousToken = null, CancellationToken cancellationToken = default)
        {
            var refresh = Convert.ToBase64String(Guid.NewGuid().ToByteArray()) + Convert.ToBase64String(Guid.NewGuid().ToByteArray());
            var now = DateTime.UtcNow;
            var expiresAt = now.AddDays(Math.Max(1, _jwtConfig.RefreshTokenDays));
            var fam = familyId ?? Guid.NewGuid().ToString("N");
            var seq = previousToken != null && _refresh.TryGetValue(previousToken, out var prev) ? prev.Sequence + 1 : 0L;
            _refresh[refresh] = (userId, deviceId, expiresAt, fam, seq, true);
            return Task.FromResult(new RefreshTokenResult { Token = refresh, FamilyId = fam, ExpiresAt = expiresAt, Sequence = seq });
        }

        public Task<bool> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            try
            {
                var principal = _handler.ValidateToken(token, _validationParams, out var validatedToken);
                if (validatedToken is not JwtSecurityToken jwt) return Task.FromResult(false);
                var jti = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;
                if (string.IsNullOrEmpty(jti)) return Task.FromResult(false);
                var revoked = _revoked.ContainsKey(jti);
                return Task.FromResult(!revoked);
            }
            catch
            {
                return Task.FromResult(false);
            }
        }

        public Task<UserClaims?> GetClaimsFromTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            try
            {
                var principal = _handler.ValidateToken(token, _validationParams, out var validatedToken);
                if (validatedToken is not JwtSecurityToken) return Task.FromResult<UserClaims?>(null);
                var claims = principal.Claims.ToList();
                var roles = claims.Where(c => c.Type == "role" || c.Type == ClaimTypes.Role).Select(c => c.Value).Distinct().ToArray();
                var perms = claims.Where(c => c.Type == "permission").Select(c => c.Value).ToArray();

                var uc = new UserClaims
                {
                    UserId = claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub || c.Type == ClaimTypes.NameIdentifier)?.Value ?? string.Empty,
                    Username = claims.FirstOrDefault(c => c.Type == "name" || c.Type == ClaimTypes.Name)?.Value ?? string.Empty,
                    Email = claims.FirstOrDefault(c => c.Type == "email" || c.Type == ClaimTypes.Email)?.Value ?? string.Empty,
                    FirstName = claims.FirstOrDefault(c => c.Type == "given_name" || c.Type == ClaimTypes.GivenName)?.Value ?? string.Empty,
                    LastName = claims.FirstOrDefault(c => c.Type == "family_name" || c.Type == ClaimTypes.Surname)?.Value ?? string.Empty,
                    Roles = roles,
                    Permissions = perms,
                    BranchId = claims.FirstOrDefault(c => c.Type == "branch_id")?.Value,
                    BranchName = claims.FirstOrDefault(c => c.Type == "branch_name")?.Value,
                    BranchRegion = claims.FirstOrDefault(c => c.Type == "branch_region")?.Value,
                    TenantId = claims.FirstOrDefault(c => c.Type == "tenant_id")?.Value,
                    SessionId = claims.FirstOrDefault(c => c.Type == "session_id")?.Value,
                    DeviceId = claims.FirstOrDefault(c => c.Type == "device_id")?.Value,
                    AuthenticationLevel = claims.FirstOrDefault(c => c.Type == "auth_level")?.Value ?? "basic",
                    IpAddress = claims.FirstOrDefault(c => c.Type == "ip_address")?.Value
                };
                return Task.FromResult<UserClaims?>(uc);
            }
            catch
            {
                return Task.FromResult<UserClaims?>(null);
            }
        }

        public Task<bool> IsTokenRevokedAsync(string tokenId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_revoked.ContainsKey(tokenId));
        }

        public Task RevokeTokenAsync(string tokenId, CancellationToken cancellationToken = default)
        {
            _revoked[tokenId] = DateTime.UtcNow;
            return Task.CompletedTask;
        }

        public Task<bool> ValidateRefreshTokenAsync(string refreshToken, string userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_refresh.TryGetValue(refreshToken, out var r) && r.Active && r.UserId == userId && r.ExpiresAt > DateTime.UtcNow);
        }

        public Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            if (_refresh.TryGetValue(refreshToken, out var r))
            {
                _refresh[refreshToken] = (r.UserId, r.DeviceId, r.ExpiresAt, r.FamilyId, r.Sequence, false);
            }
            return Task.CompletedTask;
        }

        public Task<RefreshTokenRotationResult> RefreshTokensAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
        {
            if (!_refresh.TryGetValue(request.RefreshToken, out var r) || !r.Active || r.ExpiresAt <= DateTime.UtcNow)
            {
                throw new SecurityTokenException("Invalid refresh token");
            }
            // Revoke old and issue new
            _refresh[request.RefreshToken] = (r.UserId, r.DeviceId, r.ExpiresAt, r.FamilyId, r.Sequence, false);
            var newToken = GenerateRefreshTokenAsync(r.UserId, r.DeviceId, r.FamilyId, request.RefreshToken, cancellationToken).GetAwaiter().GetResult();
            return Task.FromResult(new RefreshTokenRotationResult
            {
                UserId = r.UserId,
                DeviceId = r.DeviceId,
                FamilyId = newToken.FamilyId,
                RefreshToken = newToken.Token,
                RefreshTokenExpiresAt = newToken.ExpiresAt
            });
        }

        public Task<TokenFamilyRevocationResult?> RevokeRefreshTokenFamilyAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            if (!_refresh.TryGetValue(refreshToken, out var r)) return Task.FromResult<TokenFamilyRevocationResult?>(null);
            var familyTokens = _refresh.Where(kv => kv.Value.FamilyId == r.FamilyId).Select(kv => kv.Key).ToList();
            foreach (var t in familyTokens)
            {
                var v = _refresh[t];
                _refresh[t] = (v.UserId, v.DeviceId, v.ExpiresAt, v.FamilyId, v.Sequence, false);
            }
            return Task.FromResult<TokenFamilyRevocationResult?>(new TokenFamilyRevocationResult
            {
                FamilyId = r.FamilyId,
                UserId = r.UserId,
                RevokedTokens = familyTokens
            });
        }
    }

    private class TestSessionService : ISessionService
    {
        private readonly SessionConfiguration _config;
        private readonly ILogger<TestSessionService> _logger;
        private readonly ConcurrentDictionary<string, SessionInfo> _sessions = new();
        private readonly ConcurrentDictionary<string, HashSet<string>> _userSessions = new();

        public TestSessionService(IOptions<SessionConfiguration> sessionConfig, ILogger<TestSessionService> logger)
        {
            _config = sessionConfig.Value;
            _logger = logger;
        }

        public Task<SessionInfo> CreateSessionAsync(string userId, string username, string? deviceId = null, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default)
        {
            var sessionId = Guid.NewGuid().ToString("N");
            var now = DateTime.UtcNow;
            var info = new SessionInfo
            {
                SessionId = sessionId,
                UserId = userId,
                Username = username,
                DeviceId = deviceId,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                CreatedAt = now,
                LastAccessAt = now,
                ExpiresAt = now.AddMinutes(_config.TimeoutMinutes),
                IsActive = true
            };
            _sessions[sessionId] = info;
            _userSessions.AddOrUpdate(userId, _ => new HashSet<string> { sessionId }, (_, set) => { set.Add(sessionId); return set; });
            return Task.FromResult(info);
        }

        public Task<SessionInfo?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            _sessions.TryGetValue(sessionId, out var info);
            return Task.FromResult(info);
        }

        public Task<bool> ValidateSessionAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            var ok = _sessions.TryGetValue(sessionId, out var info) && info.IsValid;
            return Task.FromResult(ok);
        }

        public Task UpdateSessionActivityAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            if (_sessions.TryGetValue(sessionId, out var info))
            {
                var now = DateTime.UtcNow;
                info.LastAccessAt = now;
                info.ExpiresAt = now.AddMinutes(_config.TimeoutMinutes);
                _sessions[sessionId] = info;
            }
            return Task.CompletedTask;
        }

        public Task<bool> InvalidateSessionAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            if (_sessions.TryRemove(sessionId, out var info))
            {
                if (_userSessions.TryGetValue(info.UserId, out var set)) set.Remove(sessionId);
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public Task<int> InvalidateUserSessionsAsync(string userId, string? excludeSessionId = null, CancellationToken cancellationToken = default)
        {
            var count = 0;
            if (_userSessions.TryGetValue(userId, out var set))
            {
                foreach (var sid in set.ToList())
                {
                    if (excludeSessionId != null && sid == excludeSessionId) continue;
                    if (_sessions.TryRemove(sid, out _)) count++;
                }
                set.Clear();
            }
            return Task.FromResult(count);
        }

        public Task<IEnumerable<SessionInfo>> GetUserSessionsAsync(string userId, CancellationToken cancellationToken = default)
        {
            if (_userSessions.TryGetValue(userId, out var set))
            {
                var list = set.Select(sid => _sessions.TryGetValue(sid, out var info) ? info : null).Where(i => i != null && i.IsValid)!.Cast<SessionInfo>().OrderByDescending(s => s.LastAccessAt);
                return Task.FromResult<IEnumerable<SessionInfo>>(list.ToList());
            }
            return Task.FromResult<IEnumerable<SessionInfo>>(Array.Empty<SessionInfo>());
        }

        public Task<int> CleanupExpiredSessionsAsync(CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var removed = 0;
            foreach (var kv in _sessions.ToList())
            {
                if (kv.Value.ExpiresAt <= now)
                {
                    _sessions.TryRemove(kv.Key, out _);
                    removed++;
                }
            }
            return Task.FromResult(removed);
        }

        public Task<bool> ExtendSessionAsync(string sessionId, TimeSpan extension, CancellationToken cancellationToken = default)
        {
            if (_sessions.TryGetValue(sessionId, out var info))
            {
                info.ExpiresAt = info.ExpiresAt.Add(extension);
                _sessions[sessionId] = info;
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public Task<int> RevokeAllSessionsAsync(string userId, CancellationToken cancellationToken = default)
        {
            return InvalidateUserSessionsAsync(userId, null, cancellationToken);
        }
    }

    private class TestAccountLockoutService : IAccountLockoutService
    {
        private readonly AccountLockoutConfiguration _config;
        private readonly ILogger<TestAccountLockoutService> _logger;
        private readonly ConcurrentDictionary<string, (int Attempts, DateTime FirstAttemptAt, DateTime? LockedUntil)> _state = new();

        public TestAccountLockoutService(IOptions<AccountLockoutConfiguration> config, ILogger<TestAccountLockoutService> logger)
        {
            _config = config.Value;
            _logger = logger;
        }

        public Task<bool> IsAccountLockedAsync(string username, CancellationToken cancellationToken = default)
        {
            if (!_config.EnableLockout) return Task.FromResult(false);
            if (_state.TryGetValue(username, out var s) && s.LockedUntil is DateTime lu)
            {
                if (DateTime.UtcNow < lu) return Task.FromResult(true);
                // unlock expired
                _state[username] = (0, DateTime.UtcNow, null);
            }
            return Task.FromResult(false);
        }

        public Task<int> GetFailedAttemptsAsync(string username, CancellationToken cancellationToken = default)
        {
            if (_state.TryGetValue(username, out var s))
            {
                if ((DateTime.UtcNow - s.FirstAttemptAt).TotalMinutes > _config.AttemptsWindowMinutes)
                {
                    _state[username] = (0, DateTime.UtcNow, s.LockedUntil);
                    return Task.FromResult(0);
                }
                return Task.FromResult(s.Attempts);
            }
            return Task.FromResult(0);
        }

        public Task<DateTime?> GetLockoutEndAsync(string username, CancellationToken cancellationToken = default)
        {
            if (_state.TryGetValue(username, out var s)) return Task.FromResult<DateTime?>(s.LockedUntil);
            return Task.FromResult<DateTime?>(null);
        }

        public async Task RecordFailedAttemptAsync(string username, string ipAddress, CancellationToken cancellationToken = default)
        {
            if (!_config.EnableLockout) return;
            var attempts = await GetFailedAttemptsAsync(username, cancellationToken);
            attempts++;
            _state.AddOrUpdate(username, _ => (attempts, DateTime.UtcNow, null), (_, s) => (attempts, s.FirstAttemptAt == default || (DateTime.UtcNow - s.FirstAttemptAt).TotalMinutes > _config.AttemptsWindowMinutes ? DateTime.UtcNow : s.FirstAttemptAt, s.LockedUntil));
            _logger.LogWarning("Failed login attempt {Attempt} for {Username}", attempts, username);
            if (attempts >= _config.MaxFailedAttempts)
            {
                var minutes = _config.ProgressiveLockoutDurations.Length > 0 ? _config.ProgressiveLockoutDurations[0] : _config.LockoutDurationMinutes;
                var until = DateTime.UtcNow.AddMinutes(minutes);
                _state[username] = (attempts, DateTime.UtcNow, until);
                _logger.LogWarning("Account locked for {Username} until {Until}", username, until);
            }
        }

        public Task ResetFailedAttemptsAsync(string username, CancellationToken cancellationToken = default)
        {
            _state[username] = (0, DateTime.UtcNow, null);
            return Task.CompletedTask;
        }

        public Task<bool> UnlockAccountAsync(string username, CancellationToken cancellationToken = default)
        {
            _state[username] = (0, DateTime.UtcNow, null);
            return Task.FromResult(true);
        }

        public async Task<TimeSpan?> GetRemainingLockoutTimeAsync(string username, CancellationToken cancellationToken = default)
        {
            var end = await GetLockoutEndAsync(username, cancellationToken);
            if (end is null) return null;
            var remaining = end.Value - DateTime.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : null;
        }
    }
}
