using System.Security.Claims;
using IntelliFin.IdentityService.Configuration;
using IntelliFin.IdentityService.Models;
using IntelliFin.IdentityService.Services;
using IntelliFin.Shared.DomainModels.Data;
using IntelliFin.Shared.DomainModels.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntelliFin.Tests.Unit.IdentityService;

public class ServiceAccountServiceTests : IDisposable
{
    private readonly LmsDbContext _dbContext;
    private readonly Mock<IAuditService> _auditServiceMock = new();
    private readonly Mock<ILogger<ServiceAccountService>> _loggerMock = new();
    private readonly Mock<IKeycloakAdminClient> _keycloakMock = new();
    private readonly DefaultHttpContext _httpContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ServiceAccountService _sut;

    public ServiceAccountServiceTests()
    {
        var options = new DbContextOptionsBuilder<LmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new LmsDbContext(options);

        var passwordConfiguration = Options.Create(new PasswordConfiguration
        {
            SaltRounds = 12
        });

        var serviceAccountConfiguration = Options.Create(new ServiceAccountConfiguration
        {
            EnableKeycloakProvisioning = false,
            DefaultSecretLength = 48,
            ClientIdSuffixLength = 8,
            CredentialExpiryDays = 0
        });

        _httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "unit-tester"),
                new Claim(ClaimTypes.Name, "unit.tester@example.com")
            }, "Test"))
        };
        _httpContext.Request.Headers["X-Audit-Reason"] = "unit-test";
        _httpContextAccessor = new HttpContextAccessor { HttpContext = _httpContext };

        _keycloakMock
            .Setup(x => x.RegisterServiceAccountAsync(It.IsAny<ServiceAccount>(), It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((KeycloakClientRegistrationResult?)null);

        var auditEvents = new List<AuditEvent>();
        _auditServiceMock
            .Setup(x => x.LogAsync(It.IsAny<AuditEvent>(), It.IsAny<CancellationToken>()))
            .Callback<AuditEvent, CancellationToken>((evt, _) => auditEvents.Add(evt))
            .Returns(Task.CompletedTask);

        _sut = new ServiceAccountService(
            _dbContext,
            _auditServiceMock.Object,
            _loggerMock.Object,
            _httpContextAccessor,
            _keycloakMock.Object,
            passwordConfiguration,
            serviceAccountConfiguration);

        AuditEvents = auditEvents;
    }

    private List<AuditEvent> AuditEvents { get; }

    [Fact]
    public async Task CreateServiceAccountAsync_ReturnsPlainSecretOnce()
    {
        var request = new ServiceAccountCreateRequest
        {
            Name = "Payments Processor",
            Description = "Handles payment reconciliation",
            Scopes = new[] { "payments:write", "payments:read" }
        };

        var result = await _sut.CreateServiceAccountAsync(request, CancellationToken.None);

        result.Should().NotBeNull();
        result.Credential.Should().NotBeNull();
        result.Credential!.Secret.Should().NotBeNullOrWhiteSpace();
        result.Credential.Secret.Length.Should().BeGreaterOrEqualTo(32);

        var storedCredential = await _dbContext.ServiceCredentials.FirstAsync();
        storedCredential.SecretHash.Should().NotBe(result.Credential.Secret);
        storedCredential.SecretHash.Length.Should().BeGreaterThan(0);

        _loggerMock.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state?.ToString()?.Contains(result.Credential.Secret, StringComparison.Ordinal) == true),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);

        AuditEvents.Should().ContainSingle(evt => evt.Action == "service_account.created");
        AuditEvents.Single(evt => evt.Action == "service_account.created").Details?["reason"].Should().Be("unit-test");
    }

    [Fact]
    public async Task RotateSecretAsync_CreatesNewCredential_AndOldRemainsValid()
    {
        var account = await SeedServiceAccountAsync();
        var firstCredential = await _dbContext.ServiceCredentials.FirstAsync();

        var rotationResult = await _sut.RotateSecretAsync(account.Id, CancellationToken.None);

        rotationResult.Secret.Should().NotBeNullOrWhiteSpace();
        rotationResult.Secret.Length.Should().BeGreaterOrEqualTo(32);

        var credentials = await _dbContext.ServiceCredentials.Where(c => c.ServiceAccountId == account.Id).ToListAsync();
        credentials.Should().HaveCount(2);
        credentials.Should().OnlyContain(c => c.RevokedAtUtc is null);

        AuditEvents.Should().Contain(evt => evt.Action == "service_account.secret_rotated");
        AuditEvents.Last(evt => evt.Action == "service_account.secret_rotated").Details?["credentialId"].Should().NotBeNull();

        _loggerMock.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state?.ToString()?.Contains(rotationResult.Secret, StringComparison.Ordinal) == true),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);

        firstCredential.RevokedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task RevokeServiceAccountAsync_DeactivatesAccount_AndRevokesCredentials()
    {
        var account = await SeedServiceAccountAsync();
        await _sut.RotateSecretAsync(account.Id, CancellationToken.None);

        await _sut.RevokeServiceAccountAsync(account.Id, CancellationToken.None);

        var updatedAccount = await _dbContext.ServiceAccounts.Include(a => a.Credentials).FirstAsync(a => a.Id == account.Id);
        updatedAccount.IsActive.Should().BeFalse();
        updatedAccount.Credentials.Should().OnlyContain(c => c.RevokedAtUtc.HasValue);

        AuditEvents.Should().Contain(evt => evt.Action == "service_account.revoked");
        AuditEvents.Last(evt => evt.Action == "service_account.revoked").Details?["revokedCredentials"].Should().Be(2);
    }

    [Fact]
    public async Task SecretsAreNotLoggedAndMeetLengthRequirements()
    {
        var account = await SeedServiceAccountAsync();
        var rotation = await _sut.RotateSecretAsync(account.Id, CancellationToken.None);

        rotation.Secret.Length.Should().BeGreaterOrEqualTo(32);

        _loggerMock.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state?.ToString()?.Contains(rotation.Secret, StringComparison.Ordinal) == true),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    private async Task<ServiceAccount> SeedServiceAccountAsync()
    {
        var account = new ServiceAccount
        {
            ClientId = $"svc-{Guid.NewGuid():N}".Substring(0, 18),
            Name = "Seed Account",
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "seed",
            UpdatedAtUtc = DateTime.UtcNow,
            UpdatedBy = "seed"
        };
        account.SetScopes(new[] { "seed:scope" });

        var credential = new ServiceCredential
        {
            ServiceAccount = account,
            SecretHash = "hashed",
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        _dbContext.ServiceAccounts.Add(account);
        _dbContext.ServiceCredentials.Add(credential);
        await _dbContext.SaveChangesAsync();

        AuditEvents.Clear();

        return account;
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
