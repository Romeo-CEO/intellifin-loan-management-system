using System.Net;
using IntelliFin.IdentityService.Models;
using IntelliFin.IdentityService.Services;
using IntelliFin.Shared.DomainModels.Data;
using IntelliFin.Shared.DomainModels.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IntelliFin.Tests.Unit.IdentityService;

public class ServiceTokenServiceTests : IDisposable
{
    private readonly LmsDbContext _dbContext;
    private readonly Mock<IAuditService> _auditServiceMock = new();
    private readonly Mock<IKeycloakTokenClient> _keycloakTokenClientMock = new();
    private readonly Mock<ILogger<ServiceTokenService>> _loggerMock = new();
    private readonly HttpContextAccessor _httpContextAccessor;
    private readonly ServiceTokenService _sut;

    public ServiceTokenServiceTests()
    {
        var options = new DbContextOptionsBuilder<LmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new LmsDbContext(options);

        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = Guid.NewGuid().ToString()
        };
        httpContext.Request.Headers.UserAgent = "UnitTest";
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;

        _httpContextAccessor = new HttpContextAccessor { HttpContext = httpContext };

        _sut = new ServiceTokenService(
            _dbContext,
            _auditServiceMock.Object,
            _keycloakTokenClientMock.Object,
            _httpContextAccessor,
            _loggerMock.Object);
    }

    [Fact]
    public async Task GenerateTokenAsync_WithValidCredentials_ReturnsToken()
    {
        var plainSecret = new string('a', 48);
        var account = await SeedServiceAccountAsync(plainSecret, scopes: new[] { "svc:read", "svc:write" });

        _keycloakTokenClientMock
            .Setup(x => x.RequestClientCredentialsTokenAsync(account.ClientId, plainSecret, It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KeycloakTokenResponse
            {
                AccessToken = "token",
                ExpiresIn = 3600,
                Scope = "svc:read svc:write"
            });

        var response = await _sut.GenerateTokenAsync(new ClientCredentialsRequest
        {
            ClientId = account.ClientId,
            ClientSecret = plainSecret,
            Scopes = new[] { "svc:read", "svc:write" }
        });

        response.AccessToken.Should().Be("token");
        response.ExpiresIn.Should().Be(3600);
        response.TokenType.Should().Be("Bearer");
        response.Scope.Should().Be("svc:read svc:write");

        _keycloakTokenClientMock.Verify(x => x.RequestClientCredentialsTokenAsync(account.ClientId, plainSecret, It.Is<IEnumerable<string>>(s => s!.SequenceEqual(new[] { "svc:read", "svc:write" })), It.IsAny<CancellationToken>()), Times.Once);
        _auditServiceMock.Verify(x => x.LogAsync(It.Is<AuditEvent>(e => e.Action == "service_account.token_issued"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateTokenAsync_InvalidSecret_ThrowsUnauthorized()
    {
        var account = await SeedServiceAccountAsync(new string('b', 48));

        await FluentActions.Invoking(() => _sut.GenerateTokenAsync(new ClientCredentialsRequest
            {
                ClientId = account.ClientId,
                ClientSecret = new string('c', 48)
            }))
            .Should().ThrowAsync<UnauthorizedAccessException>();

        _keycloakTokenClientMock.Verify(x => x.RequestClientCredentialsTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GenerateTokenAsync_InactiveAccount_ThrowsUnauthorized()
    {
        var account = await SeedServiceAccountAsync(new string('d', 48));
        account.IsActive = false;
        _dbContext.ServiceAccounts.Update(account);
        await _dbContext.SaveChangesAsync();

        await FluentActions.Invoking(() => _sut.GenerateTokenAsync(new ClientCredentialsRequest
            {
                ClientId = account.ClientId,
                ClientSecret = new string('d', 48)
            }))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GenerateTokenAsync_RevokedCredential_ThrowsUnauthorized()
    {
        var plainSecret = new string('e', 48);
        var account = await SeedServiceAccountAsync(plainSecret);
        var credential = await _dbContext.ServiceCredentials.FirstAsync(c => c.ServiceAccountId == account.Id);
        credential.RevokedAtUtc = DateTime.UtcNow;
        _dbContext.ServiceCredentials.Update(credential);
        await _dbContext.SaveChangesAsync();

        await FluentActions.Invoking(() => _sut.GenerateTokenAsync(new ClientCredentialsRequest
            {
                ClientId = account.ClientId,
                ClientSecret = plainSecret
            }))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GenerateTokenAsync_ExpiredCredential_ThrowsUnauthorized()
    {
        var plainSecret = new string('f', 48);
        var account = await SeedServiceAccountAsync(plainSecret);
        var credential = await _dbContext.ServiceCredentials.FirstAsync(c => c.ServiceAccountId == account.Id);
        credential.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-5);
        _dbContext.ServiceCredentials.Update(credential);
        await _dbContext.SaveChangesAsync();

        await FluentActions.Invoking(() => _sut.GenerateTokenAsync(new ClientCredentialsRequest
            {
                ClientId = account.ClientId,
                ClientSecret = plainSecret
            }))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GenerateTokenAsync_UnauthorizedScope_ThrowsUnauthorized()
    {
        var plainSecret = new string('g', 48);
        var account = await SeedServiceAccountAsync(plainSecret, scopes: new[] { "svc:read" });

        await FluentActions.Invoking(() => _sut.GenerateTokenAsync(new ClientCredentialsRequest
            {
                ClientId = account.ClientId,
                ClientSecret = plainSecret,
                Scopes = new[] { "svc:read", "svc:write" }
            }))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GenerateTokenAsync_KeycloakFailure_PropagatesException()
    {
        var plainSecret = new string('h', 48);
        var account = await SeedServiceAccountAsync(plainSecret);

        _keycloakTokenClientMock
            .Setup(x => x.RequestClientCredentialsTokenAsync(account.ClientId, plainSecret, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeycloakTokenException(HttpStatusCode.BadGateway));

        await FluentActions.Invoking(() => _sut.GenerateTokenAsync(new ClientCredentialsRequest
            {
                ClientId = account.ClientId,
                ClientSecret = plainSecret
            }))
            .Should().ThrowAsync<KeycloakTokenException>();
    }

    private async Task<ServiceAccount> SeedServiceAccountAsync(string plainSecret, string[]? scopes = null)
    {
        var account = new ServiceAccount
        {
            ClientId = $"svc-{Guid.NewGuid():N}".Substring(0, 18),
            Name = "Service Account",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "seed",
            UpdatedAtUtc = DateTime.UtcNow,
            UpdatedBy = "seed"
        };
        account.SetScopes(scopes ?? Array.Empty<string>());

        var credential = new ServiceCredential
        {
            ServiceAccount = account,
            SecretHash = BCrypt.Net.BCrypt.HashPassword(plainSecret, 12),
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        _dbContext.ServiceAccounts.Add(account);
        _dbContext.ServiceCredentials.Add(credential);
        await _dbContext.SaveChangesAsync();

        return account;
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
