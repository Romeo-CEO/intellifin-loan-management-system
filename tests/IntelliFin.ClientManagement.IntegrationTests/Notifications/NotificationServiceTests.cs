using FluentAssertions;
using IntelliFin.ClientManagement.Domain.Entities;
using IntelliFin.ClientManagement.Infrastructure.Persistence;
using IntelliFin.ClientManagement.Integration;
using IntelliFin.ClientManagement.Integration.DTOs;
using IntelliFin.ClientManagement.Models;
using IntelliFin.ClientManagement.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Testcontainers.MsSql;
using Xunit;

namespace IntelliFin.ClientManagement.IntegrationTests.Notifications;

/// <summary>
/// Integration tests for notification service
/// Tests consent checking, personalization, and CommunicationsClient integration
/// </summary>
public class NotificationServiceTests : IAsyncLifetime
{
    private readonly MsSqlContainer _msSqlContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("YourStrong!Passw0rd")
        .Build();

    private ClientManagementDbContext? _context;
    private Mock<ICommunicationsClient>? _mockCommClient;
    private KycNotificationService? _notificationService;
    private Guid _testClientId;
    private Guid _testConsentId;

    public async Task InitializeAsync()
    {
        await _msSqlContainer.StartAsync();

        var options = new DbContextOptionsBuilder<ClientManagementDbContext>()
            .UseSqlServer(_msSqlContainer.GetConnectionString())
            .Options;

        _context = new ClientManagementDbContext(options);
        await _context.Database.MigrateAsync();

        // Setup mock CommunicationsClient
        _mockCommClient = new Mock<ICommunicationsClient>();
        _mockCommClient
            .Setup(c => c.SendNotificationAsync(It.IsAny<SendNotificationRequest>()))
            .ReturnsAsync(new SendNotificationResponse
            {
                NotificationId = Guid.NewGuid(),
                Status = "Queued",
                SentAt = DateTime.UtcNow,
                Channel = "SMS"
            });

        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        _notificationService = new KycNotificationService(
            _mockCommClient.Object,
            _context,
            loggerFactory.CreateLogger<KycNotificationService>());
    }

    public async Task DisposeAsync()
    {
        if (_context != null)
            await _context.DisposeAsync();
        await _msSqlContainer.DisposeAsync();
    }

    private async Task CreateTestClient(bool withConsent = true, bool smsEnabled = true)
    {
        var client = new Client
        {
            Id = Guid.NewGuid(),
            Nrc = "111111/11/1",
            FirstName = "John",
            LastName = "Banda",
            DateOfBirth = DateTime.UtcNow.AddYears(-30),
            Gender = "M",
            MaritalStatus = "Single",
            PrimaryPhone = "+260977111111",
            PhysicalAddress = "123 Test St",
            City = "Lusaka",
            Province = "Lusaka",
            BranchId = Guid.NewGuid(),
            CreatedBy = "test-user",
            UpdatedBy = "test-user"
        };

        _context!.Clients.Add(client);
        _testClientId = client.Id;

        if (withConsent)
        {
            var consent = new CommunicationConsent
            {
                Id = Guid.NewGuid(),
                ClientId = client.Id,
                ConsentType = "Operational",
                SmsEnabled = smsEnabled,
                EmailEnabled = false,
                InAppEnabled = false,
                ConsentGivenAt = DateTime.UtcNow,
                ConsentGivenBy = "test-user",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.CommunicationConsents.Add(consent);
            _testConsentId = consent.Id;
        }

        await _context.SaveChangesAsync();
    }

    #region Consent Checking Tests

    [Fact]
    public async Task CheckConsent_ClientWithSmsConsent_ReturnsTrue()
    {
        // Arrange
        await CreateTestClient(withConsent: true, smsEnabled: true);

        // Act
        var hasConsent = await _notificationService!.CheckNotificationConsentAsync(
            _testClientId, NotificationChannel.SMS);

        // Assert
        hasConsent.Should().BeTrue();
    }

    [Fact]
    public async Task CheckConsent_ClientWithoutSmsConsent_ReturnsFalse()
    {
        // Arrange
        await CreateTestClient(withConsent: true, smsEnabled: false);

        // Act
        var hasConsent = await _notificationService!.CheckNotificationConsentAsync(
            _testClientId, NotificationChannel.SMS);

        // Assert
        hasConsent.Should().BeFalse();
    }

    [Fact]
    public async Task CheckConsent_ClientWithNoConsent_ReturnsFalse()
    {
        // Arrange
        await CreateTestClient(withConsent: false);

        // Act
        var hasConsent = await _notificationService!.CheckNotificationConsentAsync(
            _testClientId, NotificationChannel.SMS);

        // Assert
        hasConsent.Should().BeFalse();
    }

    [Fact]
    public async Task CheckConsent_ClientWithRevokedConsent_ReturnsFalse()
    {
        // Arrange
        await CreateTestClient(withConsent: true, smsEnabled: true);

        // Revoke consent
        var consent = await _context!.CommunicationConsents.FindAsync(_testConsentId);
        consent!.ConsentRevokedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Act
        var hasConsent = await _notificationService!.CheckNotificationConsentAsync(
            _testClientId, NotificationChannel.SMS);

        // Assert
        hasConsent.Should().BeFalse();
    }

    #endregion

    #region Notification Sending Tests

    [Fact]
    public async Task SendNotification_ClientWithConsent_SendsSuccessfully()
    {
        // Arrange
        await CreateTestClient(withConsent: true, smsEnabled: true);

        var personalizations = new Dictionary<string, object>
        {
            ["ClientName"] = "John Banda",
            ["CompletionDate"] = "October 21, 2025",
            ["BranchContact"] = "0977-123-456"
        };

        // Act
        var result = await _notificationService!.SendKycStatusNotificationAsync(
            _testClientId,
            "kyc_approved",
            personalizations);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Success.Should().BeTrue();
        result.Value.AttemptCount.Should().BeGreaterThan(0);

        // Verify CommunicationsClient was called
        _mockCommClient!.Verify(
            c => c.SendNotificationAsync(It.Is<SendNotificationRequest>(
                r => r.TemplateId == "kyc_approved" &&
                     r.RecipientId == _testClientId.ToString() &&
                     r.Channel == "SMS")),
            Times.Once);
    }

    [Fact]
    public async Task SendNotification_ClientWithoutConsent_Blocked()
    {
        // Arrange
        await CreateTestClient(withConsent: false);

        var personalizations = new Dictionary<string, object>
        {
            ["ClientName"] = "John Banda"
        };

        // Act
        var result = await _notificationService!.SendKycStatusNotificationAsync(
            _testClientId,
            "kyc_approved",
            personalizations);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Success.Should().BeFalse();
        result.Value.BlockedReason.Should().Be("No consent");

        // Verify CommunicationsClient was NOT called
        _mockCommClient!.Verify(
            c => c.SendNotificationAsync(It.IsAny<SendNotificationRequest>()),
            Times.Never);
    }

    [Fact]
    public async Task SendNotification_PersonalizationData_ConvertedCorrectly()
    {
        // Arrange
        await CreateTestClient(withConsent: true, smsEnabled: true);

        var personalizations = new Dictionary<string, object>
        {
            ["ClientName"] = "John Banda",
            ["CompletionDate"] = DateTime.Parse("2025-10-21"),
            ["Score"] = 100
        };

        SendNotificationRequest? capturedRequest = null;
        _mockCommClient!
            .Setup(c => c.SendNotificationAsync(It.IsAny<SendNotificationRequest>()))
            .Callback<SendNotificationRequest>(r => capturedRequest = r)
            .ReturnsAsync(new SendNotificationResponse
            {
                NotificationId = Guid.NewGuid(),
                Status = "Queued",
                SentAt = DateTime.UtcNow,
                Channel = "SMS"
            });

        // Act
        await _notificationService!.SendKycStatusNotificationAsync(
            _testClientId,
            "kyc_approved",
            personalizations);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.PersonalizationData.Should().ContainKey("ClientName");
        capturedRequest.PersonalizationData["ClientName"].Should().Be("John Banda");
        capturedRequest.PersonalizationData.Should().ContainKey("CompletionDate");
        capturedRequest.PersonalizationData.Should().ContainKey("Score");
    }

    #endregion

    #region Retry Logic Tests

    [Fact]
    public async Task SendNotification_TransientFailure_RetriesAndSucceeds()
    {
        // Arrange
        await CreateTestClient(withConsent: true, smsEnabled: true);

        var attemptCount = 0;
        _mockCommClient!
            .Setup(c => c.SendNotificationAsync(It.IsAny<SendNotificationRequest>()))
            .Returns(async () =>
            {
                attemptCount++;
                if (attemptCount < 2)
                {
                    await Task.Delay(10);
                    throw new HttpRequestException("Transient error");
                }

                return new SendNotificationResponse
                {
                    NotificationId = Guid.NewGuid(),
                    Status = "Queued",
                    SentAt = DateTime.UtcNow,
                    Channel = "SMS"
                };
            });

        var personalizations = new Dictionary<string, object>
        {
            ["ClientName"] = "John Banda"
        };

        // Act
        var result = await _notificationService!.SendKycStatusNotificationAsync(
            _testClientId,
            "kyc_approved",
            personalizations);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Success.Should().BeTrue();
        result.Value.AttemptCount.Should().BeGreaterThanOrEqualTo(2);
        attemptCount.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task SendNotification_PermanentFailure_MarksAsDlq()
    {
        // Arrange
        await CreateTestClient(withConsent: true, smsEnabled: true);

        _mockCommClient!
            .Setup(c => c.SendNotificationAsync(It.IsAny<SendNotificationRequest>()))
            .ThrowsAsync(new Exception("Permanent failure"));

        var personalizations = new Dictionary<string, object>
        {
            ["ClientName"] = "John Banda"
        };

        // Act
        var result = await _notificationService!.SendKycStatusNotificationAsync(
            _testClientId,
            "kyc_approved",
            personalizations);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Success.Should().BeFalse();
        result.Value.SentToDlq.Should().BeTrue();
        result.Value.FinalError.Should().Contain("Permanent failure");
    }

    #endregion
}
