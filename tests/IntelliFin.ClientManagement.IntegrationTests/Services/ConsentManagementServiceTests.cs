using IntelliFin.ClientManagement.Controllers.DTOs;
using IntelliFin.ClientManagement.Domain.Entities;
using IntelliFin.ClientManagement.Infrastructure.Persistence;
using IntelliFin.ClientManagement.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Testcontainers.MsSql;

namespace IntelliFin.ClientManagement.IntegrationTests.Services;

/// <summary>
/// Integration tests for ConsentManagementService
/// Tests consent creation, retrieval, update, and revocation
/// </summary>
public class ConsentManagementServiceTests : IAsyncLifetime
{
    private readonly MsSqlContainer _msSqlContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("YourStrong!Passw0rd")
        .Build();

    private ClientManagementDbContext? _context;
    private ConsentManagementService? _service;
    private Mock<IAuditService>? _mockAuditService;
    private Guid _testClientId;

    public async Task InitializeAsync()
    {
        await _msSqlContainer.StartAsync();

        var options = new DbContextOptionsBuilder<ClientManagementDbContext>()
            .UseSqlServer(_msSqlContainer.GetConnectionString())
            .Options;

        _context = new ClientManagementDbContext(options);
        await _context.Database.MigrateAsync();

        // Create a test client
        var testClient = new Client
        {
            Id = Guid.NewGuid(),
            Nrc = "123456/78/9",
            FirstName = "Test",
            LastName = "User",
            DateOfBirth = new DateTime(1990, 1, 1),
            Gender = "M",
            MaritalStatus = "Single",
            PrimaryPhone = "+260977123456",
            PhysicalAddress = "123 Test St",
            City = "Lusaka",
            Province = "Lusaka",
            BranchId = Guid.NewGuid(),
            CreatedBy = "test-user",
            UpdatedBy = "test-user"
        };

        _context.Clients.Add(testClient);
        await _context.SaveChangesAsync();
        _testClientId = testClient.Id;

        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<ConsentManagementService>();

        _mockAuditService = new Mock<IAuditService>();

        _service = new ConsentManagementService(
            _context,
            _mockAuditService.Object,
            logger);
    }

    public async Task DisposeAsync()
    {
        if (_context != null)
            await _context.DisposeAsync();
        await _msSqlContainer.DisposeAsync();
    }

    [Fact]
    public async Task UpdateConsentAsync_NewConsent_ShouldCreateRecord()
    {
        // Arrange
        var request = new UpdateConsentRequest
        {
            ConsentType = "Operational",
            SmsEnabled = true,
            EmailEnabled = true,
            InAppEnabled = false,
            CallEnabled = false
        };

        // Act
        var result = await _service!.UpdateConsentAsync(_testClientId, request, "test-user");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.ConsentType.Should().Be("Operational");
        result.Value.SmsEnabled.Should().BeTrue();
        result.Value.EmailEnabled.Should().BeTrue();
        result.Value.InAppEnabled.Should().BeFalse();
        result.Value.CallEnabled.Should().BeFalse();
        result.Value.IsActive.Should().BeTrue();
        result.Value.ConsentGivenBy.Should().Be("test-user");

        // Verify record created in database
        var consent = await _context!.CommunicationConsents
            .FirstOrDefaultAsync(c => c.ClientId == _testClientId && c.ConsentType == "Operational");
        consent.Should().NotBeNull();

        // Verify audit event logged
        _mockAuditService!.Verify(
            x => x.LogAuditEventAsync(
                "ConsentUpdated",
                "CommunicationConsent",
                It.IsAny<string>(),
                "test-user",
                It.IsAny<object>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateConsentAsync_RevokeConsent_ShouldSetRevokedAt()
    {
        // Arrange - Create initial consent
        var createRequest = new UpdateConsentRequest
        {
            ConsentType = "Marketing",
            SmsEnabled = true,
            EmailEnabled = true,
            InAppEnabled = true,
            CallEnabled = true
        };
        await _service!.UpdateConsentAsync(_testClientId, createRequest, "user1");

        // Act - Revoke consent (all channels disabled)
        var revokeRequest = new UpdateConsentRequest
        {
            ConsentType = "Marketing",
            SmsEnabled = false,
            EmailEnabled = false,
            InAppEnabled = false,
            CallEnabled = false,
            RevocationReason = "Customer opted out"
        };
        var result = await _service.UpdateConsentAsync(_testClientId, revokeRequest, "user2");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.IsActive.Should().BeFalse();
        result.Value.ConsentRevokedAt.Should().NotBeNull();
        result.Value.RevocationReason.Should().Be("Customer opted out");
        result.Value.SmsEnabled.Should().BeFalse();
        result.Value.EmailEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateConsentAsync_ReGrantConsent_ShouldClearRevocation()
    {
        // Arrange - Create and revoke consent
        var createRequest = new UpdateConsentRequest
        {
            ConsentType = "Operational",
            SmsEnabled = true,
            EmailEnabled = true,
            InAppEnabled = false,
            CallEnabled = false
        };
        await _service!.UpdateConsentAsync(_testClientId, createRequest, "user1");

        var revokeRequest = new UpdateConsentRequest
        {
            ConsentType = "Operational",
            SmsEnabled = false,
            EmailEnabled = false,
            InAppEnabled = false,
            CallEnabled = false,
            RevocationReason = "Test revocation"
        };
        await _service.UpdateConsentAsync(_testClientId, revokeRequest, "user2");

        // Act - Re-grant consent
        var regrantRequest = new UpdateConsentRequest
        {
            ConsentType = "Operational",
            SmsEnabled = true,
            EmailEnabled = false,
            InAppEnabled = true,
            CallEnabled = false
        };
        var result = await _service.UpdateConsentAsync(_testClientId, regrantRequest, "user3");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.IsActive.Should().BeTrue();
        result.Value.ConsentRevokedAt.Should().BeNull();
        result.Value.RevocationReason.Should().BeNull();
        result.Value.SmsEnabled.Should().BeTrue();
        result.Value.InAppEnabled.Should().BeTrue();
        result.Value.EmailEnabled.Should().BeFalse();
        result.Value.ConsentGivenBy.Should().Be("user3");
    }

    [Fact]
    public async Task CheckConsentAsync_ConsentGranted_ShouldReturnTrue()
    {
        // Arrange - Create consent with SMS enabled
        var request = new UpdateConsentRequest
        {
            ConsentType = "Operational",
            SmsEnabled = true,
            EmailEnabled = false,
            InAppEnabled = false,
            CallEnabled = false
        };
        await _service!.UpdateConsentAsync(_testClientId, request, "test-user");

        // Act
        var hasConsent = await _service.CheckConsentAsync(_testClientId, "Operational", "SMS");

        // Assert
        hasConsent.Should().BeTrue();
    }

    [Fact]
    public async Task CheckConsentAsync_ConsentRevoked_ShouldReturnFalse()
    {
        // Arrange - Create and revoke consent
        var createRequest = new UpdateConsentRequest
        {
            ConsentType = "Marketing",
            SmsEnabled = true,
            EmailEnabled = true,
            InAppEnabled = false,
            CallEnabled = false
        };
        await _service!.UpdateConsentAsync(_testClientId, createRequest, "user1");

        var revokeRequest = new UpdateConsentRequest
        {
            ConsentType = "Marketing",
            SmsEnabled = false,
            EmailEnabled = false,
            InAppEnabled = false,
            CallEnabled = false,
            RevocationReason = "Test"
        };
        await _service.UpdateConsentAsync(_testClientId, revokeRequest, "user2");

        // Act
        var hasConsent = await _service.CheckConsentAsync(_testClientId, "Marketing", "SMS");

        // Assert
        hasConsent.Should().BeFalse();
    }

    [Fact]
    public async Task CheckConsentAsync_ConsentNotFound_ShouldReturnFalse()
    {
        // Act
        var hasConsent = await _service!.CheckConsentAsync(_testClientId, "Operational", "SMS");

        // Assert - Default deny
        hasConsent.Should().BeFalse();
    }

    [Fact]
    public async Task CheckConsentAsync_ChannelDisabled_ShouldReturnFalse()
    {
        // Arrange - Create consent with only SMS enabled
        var request = new UpdateConsentRequest
        {
            ConsentType = "Operational",
            SmsEnabled = true,
            EmailEnabled = false,
            InAppEnabled = false,
            CallEnabled = false
        };
        await _service!.UpdateConsentAsync(_testClientId, request, "test-user");

        // Act - Check email (which is disabled)
        var hasConsent = await _service.CheckConsentAsync(_testClientId, "Operational", "Email");

        // Assert
        hasConsent.Should().BeFalse();
    }

    [Fact]
    public async Task GetConsentAsync_WhenExists_ShouldReturnConsent()
    {
        // Arrange - Create consent
        var request = new UpdateConsentRequest
        {
            ConsentType = "Operational",
            SmsEnabled = true,
            EmailEnabled = true,
            InAppEnabled = false,
            CallEnabled = false
        };
        await _service!.UpdateConsentAsync(_testClientId, request, "test-user");

        // Act
        var result = await _service.GetConsentAsync(_testClientId, "Operational");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.ConsentType.Should().Be("Operational");
        result.Value.SmsEnabled.Should().BeTrue();
        result.Value.EmailEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task GetConsentAsync_WhenNotExists_ShouldReturnNull()
    {
        // Act
        var result = await _service!.GetConsentAsync(_testClientId, "Marketing");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task GetAllConsentsAsync_ShouldReturnAllConsents()
    {
        // Arrange - Create multiple consents
        var operationalRequest = new UpdateConsentRequest
        {
            ConsentType = "Operational",
            SmsEnabled = true,
            EmailEnabled = true,
            InAppEnabled = false,
            CallEnabled = false
        };
        await _service!.UpdateConsentAsync(_testClientId, operationalRequest, "user1");

        var marketingRequest = new UpdateConsentRequest
        {
            ConsentType = "Marketing",
            SmsEnabled = false,
            EmailEnabled = true,
            InAppEnabled = true,
            CallEnabled = false
        };
        await _service.UpdateConsentAsync(_testClientId, marketingRequest, "user2");

        // Act
        var result = await _service.GetAllConsentsAsync(_testClientId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Count.Should().Be(2);
        result.Value.Should().Contain(c => c.ConsentType == "Operational");
        result.Value.Should().Contain(c => c.ConsentType == "Marketing");
    }

    [Fact]
    public async Task UpdateConsentAsync_NonExistentClient_ShouldFail()
    {
        // Arrange
        var nonExistentClientId = Guid.NewGuid();
        var request = new UpdateConsentRequest
        {
            ConsentType = "Operational",
            SmsEnabled = true,
            EmailEnabled = true,
            InAppEnabled = false,
            CallEnabled = false
        };

        // Act
        var result = await _service!.UpdateConsentAsync(nonExistentClientId, request, "test-user");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task UpdateConsentAsync_UniqueConstraint_ShouldUpdateExisting()
    {
        // Arrange - Create initial consent
        var request1 = new UpdateConsentRequest
        {
            ConsentType = "Operational",
            SmsEnabled = true,
            EmailEnabled = false,
            InAppEnabled = false,
            CallEnabled = false
        };
        await _service!.UpdateConsentAsync(_testClientId, request1, "user1");

        // Act - Update same consent type
        var request2 = new UpdateConsentRequest
        {
            ConsentType = "Operational",
            SmsEnabled = true,
            EmailEnabled = true,
            InAppEnabled = true,
            CallEnabled = false
        };
        var result = await _service.UpdateConsentAsync(_testClientId, request2, "user2");

        // Assert - Should update, not create new
        result.IsSuccess.Should().BeTrue();
        result.Value!.EmailEnabled.Should().BeTrue();
        result.Value.InAppEnabled.Should().BeTrue();

        // Verify only one record exists
        var consents = await _context!.CommunicationConsents
            .Where(c => c.ClientId == _testClientId && c.ConsentType == "Operational")
            .ToListAsync();
        consents.Count.Should().Be(1);
    }
}
