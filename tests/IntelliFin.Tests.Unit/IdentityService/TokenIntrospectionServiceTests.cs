using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using IntelliFin.IdentityService.Configuration;
using IntelliFin.IdentityService.Models;
using IntelliFin.IdentityService.Services;
using IntelliFin.Shared.DomainModels.Data;
using IntelliFin.Shared.DomainModels.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using StackExchange.Redis;

namespace IntelliFin.Tests.Unit.IdentityService;

public class TokenIntrospectionServiceTests : IDisposable
{
    private readonly LmsDbContext _dbContext;
    private readonly Mock<IAuditService> _auditServiceMock = new();
    private readonly Mock<IConnectionMultiplexer> _multiplexerMock = new();
    private readonly Mock<IDatabase> _databaseMock = new();
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock = new();
    private readonly JwtConfiguration _jwtConfig;
    private readonly AuthorizationConfiguration _authConfig;
    private readonly RedisConfiguration _redisConfig = new();

    public TokenIntrospectionServiceTests()
    {
        var options = new DbContextOptionsBuilder<LmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new LmsDbContext(options);

        _databaseMock
            .Setup(db => db.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(false);

        _multiplexerMock
            .Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_databaseMock.Object);

        _httpClientFactoryMock
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(new FakeHttpMessageHandler()));

        _jwtConfig = new JwtConfiguration
        {
            Issuer = "IntelliFin.Identity",
            Audience = "intellifin-api",
            SigningKey = "test-signing-key-which-is-long-enough"
        };

        _authConfig = new AuthorizationConfiguration
        {
            TrustedIssuers = new[] { _jwtConfig.Issuer },
            AllowedAudiences = new[] { _jwtConfig.Audience },
            RequireHttpsMetadata = false
        };
    }

    [Fact]
    public async Task IntrospectAsync_WithValidToken_ReturnsActiveResponse()
    {
        var subjectId = Guid.NewGuid().ToString();
        var branchId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var jti = Guid.NewGuid().ToString();

        await SeedUserAsync(subjectId, branchId.ToString(), tenantId.ToString());

        var token = CreateToken(subjectId, jti, branchId, tenantId);
        var service = CreateService();

        var response = await service.IntrospectAsync(new IntrospectionRequest { Token = token });

        response.Active.Should().BeTrue();
        response.Subject.Should().Be(subjectId);
        response.BranchId.Should().Be(branchId);
        response.TenantId.Should().Be(tenantId);
        response.Roles.Should().Contain(new[] { "platform_admin", "initial-role" });
        response.Permissions.Should().Contain(new[] { "system:users_manage", "initial:perm" });
        response.Issuer.Should().Be(_jwtConfig.Issuer);

        _auditServiceMock.Verify(a => a.LogAsync(It.Is<AuditEvent>(e => e.Success), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IntrospectAsync_WhenTokenRevoked_ReturnsInactive()
    {
        var subjectId = Guid.NewGuid().ToString();
        var jti = Guid.NewGuid().ToString();
        var token = CreateToken(subjectId, jti, Guid.NewGuid(), Guid.NewGuid());

        _dbContext.TokenRevocations.Add(new TokenRevocation
        {
            TokenId = jti,
            RevokedAtUtc = DateTime.UtcNow,
            RevokedBy = "tester"
        });
        await _dbContext.SaveChangesAsync();

        var service = CreateService();

        var response = await service.IntrospectAsync(new IntrospectionRequest { Token = token });

        response.Active.Should().BeFalse();
        _auditServiceMock.Verify(a => a.LogAsync(It.Is<AuditEvent>(e => !e.Success && e.Details.ContainsKey("reason")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IntrospectAsync_WithUnknownIssuer_Throws()
    {
        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateEncodedJwt(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[] { new Claim(JwtRegisteredClaimNames.Sub, "abc") }),
            Expires = DateTime.UtcNow.AddMinutes(5),
            Audience = _jwtConfig.Audience,
            Issuer = "https://unknown",
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtConfig.SigningKey)), SecurityAlgorithms.HmacSha256)
        });

        var service = CreateService();

        await FluentActions.Invoking(() => service.IntrospectAsync(new IntrospectionRequest { Token = token }))
            .Should().ThrowAsync<UnknownIssuerException>();
    }

    private TokenIntrospectionService CreateService()
    {
        return new TokenIntrospectionService(
            Options.Create(_jwtConfig),
            Options.Create(_authConfig),
            Options.Create(_redisConfig),
            _dbContext,
            _auditServiceMock.Object,
            _multiplexerMock.Object,
            _httpClientFactoryMock.Object,
            Mock.Of<ILogger<TokenIntrospectionService>>());
    }

    private async Task SeedUserAsync(string userId, string branchId, string tenantId)
    {
        var role = new Role
        {
            Id = Guid.NewGuid().ToString(),
            Name = "platform_admin",
            IsActive = true,
            RolePermissions = new List<RolePermission>
            {
                new()
                {
                    RoleId = Guid.NewGuid().ToString(),
                    Permission = new Permission
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = "system:users_manage",
                        IsActive = true
                    },
                    IsActive = true
                }
            }
        };

        var user = new User
        {
            Id = userId,
            Username = "jdoe",
            Email = "jdoe@example.com",
            BranchId = branchId,
            Metadata = new Dictionary<string, object>
            {
                ["tenantId"] = tenantId
            },
            UserRoles = new List<UserRole>
            {
                new()
                {
                    UserId = userId,
                    RoleId = role.Id,
                    Role = role,
                    IsActive = true
                }
            }
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
    }

    private string CreateToken(string subject, string jti, Guid branchId, Guid tenantId)
    {
        var handler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtConfig.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = handler.CreateJwtSecurityToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, subject),
                new Claim(JwtRegisteredClaimNames.Jti, jti),
                new Claim(ClaimTypes.Name, "jdoe"),
                new Claim(ClaimTypes.Email, "jdoe@example.com"),
                new Claim(ClaimTypes.Role, "initial-role"),
                new Claim("permission", "initial:perm"),
                new Claim("branch_id", branchId.ToString()),
                new Claim("tenant_id", tenantId.ToString())
            }),
            Expires = DateTime.UtcNow.AddMinutes(10),
            Audience = _jwtConfig.Audience,
            Issuer = _jwtConfig.Issuer,
            SigningCredentials = credentials
        });

        return handler.WriteToken(token);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
