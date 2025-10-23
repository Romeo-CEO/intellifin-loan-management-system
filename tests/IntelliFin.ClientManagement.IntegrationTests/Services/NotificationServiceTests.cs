using IntelliFin.ClientManagement.Integration;
using IntelliFin.ClientManagement.Integration.DTOs;
using IntelliFin.ClientManagement.Services;
using Microsoft.Extensions.Logging;

namespace IntelliFin.ClientManagement.IntegrationTests.Services;

/// <summary>
/// Integration tests for NotificationService
/// Tests consent-based notification sending
/// </summary>
public class NotificationServiceTests
{
    [Fact]
    public async Task SendConsentBasedNotificationAsync_WithConsent_ShouldSendNotification()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var mockCommunicationsClient = new Mock<ICommunicationsClient>();
        var mockConsentService = new Mock<IConsentManagementService>();
        var mockAuditService = new Mock<IAuditService>();
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<NotificationService>();

        // Setup: Consent granted
        mockConsentService
            .Setup(x => x.CheckConsentAsync(clientId, "Operational", "SMS"))
            .ReturnsAsync(true);

        // Setup: CommunicationsService response
        var expectedResponse = new SendNotificationResponse
        {
            NotificationId = Guid.NewGuid(),
            Status = "Queued",
            SentAt = DateTime.UtcNow,
            Channel = "SMS"
        };

        mockCommunicationsClient
            .Setup(x => x.SendNotificationAsync(It.IsAny<SendNotificationRequest>()))
            .ReturnsAsync(expectedResponse);

        var service = new NotificationService(
            mockCommunicationsClient.Object,
            mockConsentService.Object,
            mockAuditService.Object,
            logger);

        var personalizationData = new Dictionary<string, string>
        {
            { "clientName", "John Doe" },
            { "kycStatus", "Approved" }
        };

        // Act
        var result = await service.SendConsentBasedNotificationAsync(
            clientId,
            "kyc_approved",
            "Operational",
            "SMS",
            personalizationData,
            "test-user");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.NotificationId.Should().Be(expectedResponse.NotificationId);
        result.Value.Status.Should().Be("Queued");

        // Verify CommunicationsService was called
        mockCommunicationsClient.Verify(
            x => x.SendNotificationAsync(It.Is<SendNotificationRequest>(r =>
                r.TemplateId == "kyc_approved" &&
                r.Channel == "SMS" &&
                r.RecipientId == clientId.ToString())),
            Times.Once);

        // Verify audit event logged
        mockAuditService.Verify(
            x => x.LogAuditEventAsync(
                "NotificationSent",
                "Notification",
                It.IsAny<string>(),
                "test-user",
                It.IsAny<object>()),
            Times.Once);
    }

    [Fact]
    public async Task SendConsentBasedNotificationAsync_WithoutConsent_ShouldNotSendNotification()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var mockCommunicationsClient = new Mock<ICommunicationsClient>();
        var mockConsentService = new Mock<IConsentManagementService>();
        var mockAuditService = new Mock<IAuditService>();
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<NotificationService>();

        // Setup: Consent NOT granted
        mockConsentService
            .Setup(x => x.CheckConsentAsync(clientId, "Marketing", "Email"))
            .ReturnsAsync(false);

        var service = new NotificationService(
            mockCommunicationsClient.Object,
            mockConsentService.Object,
            mockAuditService.Object,
            logger);

        var personalizationData = new Dictionary<string, string>
        {
            { "clientName", "Jane Doe" },
            { "offerType", "Personal Loan" }
        };

        // Act
        var result = await service.SendConsentBasedNotificationAsync(
            clientId,
            "product_offer",
            "Marketing",
            "Email",
            personalizationData,
            "test-user");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull(); // No notification sent

        // Verify CommunicationsService was NOT called
        mockCommunicationsClient.Verify(
            x => x.SendNotificationAsync(It.IsAny<SendNotificationRequest>()),
            Times.Never);

        // Verify NO audit event logged
        mockAuditService.Verify(
            x => x.LogAuditEventAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>()),
            Times.Never);
    }

    [Fact]
    public async Task SendNotificationAsync_RegulatoryNotification_ShouldBypassConsent()
    {
        // Arrange
        var mockCommunicationsClient = new Mock<ICommunicationsClient>();
        var mockConsentService = new Mock<IConsentManagementService>();
        var mockAuditService = new Mock<IAuditService>();
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<NotificationService>();

        // Setup: CommunicationsService response
        var expectedResponse = new SendNotificationResponse
        {
            NotificationId = Guid.NewGuid(),
            Status = "Sent",
            SentAt = DateTime.UtcNow,
            Channel = "SMS"
        };

        mockCommunicationsClient
            .Setup(x => x.SendNotificationAsync(It.IsAny<SendNotificationRequest>()))
            .ReturnsAsync(expectedResponse);

        var service = new NotificationService(
            mockCommunicationsClient.Object,
            mockConsentService.Object,
            mockAuditService.Object,
            logger);

        var request = new SendNotificationRequest
        {
            TemplateId = "regulatory_notice",
            RecipientId = Guid.NewGuid().ToString(),
            Channel = "SMS",
            PersonalizationData = new Dictionary<string, string>
            {
                { "clientName", "John Doe" },
                { "noticeType", "Terms Update" }
            }
        };

        // Act
        var result = await service.SendNotificationAsync(request, "system");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.NotificationId.Should().Be(expectedResponse.NotificationId);

        // Verify CommunicationsService was called (bypassing consent check)
        mockCommunicationsClient.Verify(
            x => x.SendNotificationAsync(It.Is<SendNotificationRequest>(r =>
                r.TemplateId == "regulatory_notice")),
            Times.Once);

        // Verify consent service was NOT called
        mockConsentService.Verify(
            x => x.CheckConsentAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);

        // Verify audit event logged with ConsentBypass flag
        mockAuditService.Verify(
            x => x.LogAuditEventAsync(
                "NotificationSent",
                "Notification",
                It.IsAny<string>(),
                "system",
                It.Is<object>(data => data.ToString()!.Contains("ConsentBypass"))),
            Times.Once);
    }

    [Fact]
    public async Task SendConsentBasedNotificationAsync_CommunicationsServiceUnavailable_ShouldReturnFailure()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var mockCommunicationsClient = new Mock<ICommunicationsClient>();
        var mockConsentService = new Mock<IConsentManagementService>();
        var mockAuditService = new Mock<IAuditService>();
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<NotificationService>();

        // Setup: Consent granted
        mockConsentService
            .Setup(x => x.CheckConsentAsync(clientId, "Operational", "Email"))
            .ReturnsAsync(true);

        // Setup: CommunicationsService throws exception
        mockCommunicationsClient
            .Setup(x => x.SendNotificationAsync(It.IsAny<SendNotificationRequest>()))
            .ThrowsAsync(new HttpRequestException("Service unavailable"));

        var service = new NotificationService(
            mockCommunicationsClient.Object,
            mockConsentService.Object,
            mockAuditService.Object,
            logger);

        var personalizationData = new Dictionary<string, string>
        {
            { "clientName", "Test User" }
        };

        // Act
        var result = await service.SendConsentBasedNotificationAsync(
            clientId,
            "test_template",
            "Operational",
            "Email",
            personalizationData,
            "test-user");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Failed to send notification");

        // Verify NO audit event logged (notification failed)
        mockAuditService.Verify(
            x => x.LogAuditEventAsync(
                "NotificationSent",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>()),
            Times.Never);
    }

    [Fact]
    public async Task SendConsentBasedNotificationAsync_ChecksCorrectChannel()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var mockCommunicationsClient = new Mock<ICommunicationsClient>();
        var mockConsentService = new Mock<IConsentManagementService>();
        var mockAuditService = new Mock<IAuditService>();
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<NotificationService>();

        // Setup: Only SMS consented, not Email
        mockConsentService
            .Setup(x => x.CheckConsentAsync(clientId, "Operational", "SMS"))
            .ReturnsAsync(true);
        mockConsentService
            .Setup(x => x.CheckConsentAsync(clientId, "Operational", "Email"))
            .ReturnsAsync(false);

        var service = new NotificationService(
            mockCommunicationsClient.Object,
            mockConsentService.Object,
            mockAuditService.Object,
            logger);

        var personalizationData = new Dictionary<string, string>
        {
            { "clientName", "Test User" }
        };

        // Act - Try to send via Email (not consented)
        var emailResult = await service.SendConsentBasedNotificationAsync(
            clientId,
            "test_template",
            "Operational",
            "Email",
            personalizationData,
            "test-user");

        // Assert - Email notification blocked
        emailResult.IsSuccess.Should().BeTrue();
        emailResult.Value.Should().BeNull();

        // Verify consent was checked for correct channel
        mockConsentService.Verify(
            x => x.CheckConsentAsync(clientId, "Operational", "Email"),
            Times.Once);

        // Verify CommunicationsService NOT called
        mockCommunicationsClient.Verify(
            x => x.SendNotificationAsync(It.IsAny<SendNotificationRequest>()),
            Times.Never);
    }
}
