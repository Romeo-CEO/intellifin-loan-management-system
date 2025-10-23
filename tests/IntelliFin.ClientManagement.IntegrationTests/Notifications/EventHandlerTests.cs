using FluentAssertions;
using IntelliFin.ClientManagement.Domain.Entities;
using IntelliFin.ClientManagement.Domain.Events;
using IntelliFin.ClientManagement.EventHandlers;
using IntelliFin.ClientManagement.Infrastructure.Persistence;
using IntelliFin.ClientManagement.Integration;
using IntelliFin.ClientManagement.Integration.DTOs;
using IntelliFin.ClientManagement.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Testcontainers.MsSql;
using Xunit;

namespace IntelliFin.ClientManagement.IntegrationTests.Notifications;

/// <summary>
/// Integration tests for event handlers
/// Tests the complete flow from domain event to notification
/// </summary>
public class EventHandlerTests : IAsyncLifetime
{
    private readonly MsSqlContainer _msSqlContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("YourStrong!Passw0rd")
        .Build();

    private ClientManagementDbContext? _context;
    private Mock<ICommunicationsClient>? _mockCommClient;
    private Guid _testClientId;

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
    }

    public async Task DisposeAsync()
    {
        if (_context != null)
            await _context.DisposeAsync();
        await _msSqlContainer.DisposeAsync();
    }

    private async Task CreateTestClientWithConsent()
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

        var consent = new CommunicationConsent
        {
            Id = Guid.NewGuid(),
            ClientId = client.Id,
            ConsentType = "Operational",
            SmsEnabled = true,
            EmailEnabled = false,
            InAppEnabled = false,
            ConsentGivenAt = DateTime.UtcNow,
            ConsentGivenBy = "test-user",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context!.Clients.Add(client);
        _context.CommunicationConsents.Add(consent);
        await _context.SaveChangesAsync();

        _testClientId = client.Id;
    }

    #region KYC Completed Event Handler Tests

    [Fact]
    public async Task KycCompletedEvent_WithConsent_SendsNotification()
    {
        // Arrange
        await CreateTestClientWithConsent();

        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var notificationService = new KycNotificationService(
            _mockCommClient!.Object,
            _context!,
            loggerFactory.CreateLogger<KycNotificationService>());
        var templatePersonalizer = new TemplatePersonalizer(
            loggerFactory.CreateLogger<TemplatePersonalizer>());

        var handler = new KycCompletedEventHandler(
            notificationService,
            templatePersonalizer,
            loggerFactory.CreateLogger<KycCompletedEventHandler>());

        var domainEvent = new KycCompletedEvent
        {
            ClientId = _testClientId,
            KycStatusId = Guid.NewGuid(),
            ClientName = "John Banda",
            CompletedAt = DateTime.UtcNow,
            CompletedBy = "system-workflow",
            RiskRating = "Low",
            RiskScore = 15,
            EddRequired = false,
            CorrelationId = "test-correlation"
        };

        // Act
        await handler.HandleAsync(domainEvent);

        // Wait a bit for async handler
        await Task.Delay(100);

        // Assert
        _mockCommClient.Verify(
            c => c.SendNotificationAsync(It.Is<SendNotificationRequest>(
                r => r.TemplateId == "kyc_approved" &&
                     r.RecipientId == _testClientId.ToString() &&
                     r.Channel == "SMS" &&
                     r.PersonalizationData.ContainsKey("ClientName"))),
            Times.Once);
    }

    #endregion

    #region KYC Rejected Event Handler Tests

    [Fact]
    public async Task KycRejectedEvent_WithConsent_SendsNotification()
    {
        // Arrange
        await CreateTestClientWithConsent();

        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var notificationService = new KycNotificationService(
            _mockCommClient!.Object,
            _context!,
            loggerFactory.CreateLogger<KycNotificationService>());
        var templatePersonalizer = new TemplatePersonalizer(
            loggerFactory.CreateLogger<TemplatePersonalizer>());

        var handler = new KycRejectedEventHandler(
            notificationService,
            templatePersonalizer,
            loggerFactory.CreateLogger<KycRejectedEventHandler>());

        var domainEvent = new KycRejectedEvent
        {
            ClientId = _testClientId,
            KycStatusId = Guid.NewGuid(),
            ClientName = "John Banda",
            RejectedAt = DateTime.UtcNow,
            RejectedBy = "compliance-officer",
            RejectionStage = "Compliance",
            RejectionReason = "Incomplete Documents",
            CanReapply = true,
            CorrelationId = "test-correlation"
        };

        // Act
        await handler.HandleAsync(domainEvent);

        // Wait for async handler
        await Task.Delay(100);

        // Assert
        _mockCommClient.Verify(
            c => c.SendNotificationAsync(It.Is<SendNotificationRequest>(
                r => r.TemplateId == "kyc_rejected" &&
                     r.RecipientId == _testClientId.ToString() &&
                     r.PersonalizationData.ContainsKey("RejectionReason"))),
            Times.Once);
    }

    #endregion

    #region EDD Escalated Event Handler Tests

    [Fact]
    public async Task EddEscalatedEvent_WithConsent_SendsNotification()
    {
        // Arrange
        await CreateTestClientWithConsent();

        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var notificationService = new KycNotificationService(
            _mockCommClient!.Object,
            _context!,
            loggerFactory.CreateLogger<KycNotificationService>());
        var templatePersonalizer = new TemplatePersonalizer(
            loggerFactory.CreateLogger<TemplatePersonalizer>());

        var handler = new EddEscalatedEventHandler(
            notificationService,
            templatePersonalizer,
            loggerFactory.CreateLogger<EddEscalatedEventHandler>());

        var domainEvent = new EddEscalatedEvent
        {
            ClientId = _testClientId,
            KycStatusId = Guid.NewGuid(),
            ClientName = "John Banda",
            EscalatedAt = DateTime.UtcNow,
            EddReason = "High Risk",
            RiskLevel = "High",
            HasSanctionsHit = false,
            IsPep = true,
            ExpectedTimeframe = "5-7 business days",
            CorrelationId = "test-correlation"
        };

        // Act
        await handler.HandleAsync(domainEvent);

        // Wait for async handler
        await Task.Delay(100);

        // Assert
        _mockCommClient.Verify(
            c => c.SendNotificationAsync(It.Is<SendNotificationRequest>(
                r => r.TemplateId == "kyc_edd_required" &&
                     r.RecipientId == _testClientId.ToString() &&
                     r.PersonalizationData.ContainsKey("ExpectedTimeframe"))),
            Times.Once);
    }

    #endregion

    #region EDD Approved Event Handler Tests

    [Fact]
    public async Task EddApprovedEvent_WithConsent_SendsNotification()
    {
        // Arrange
        await CreateTestClientWithConsent();

        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var notificationService = new KycNotificationService(
            _mockCommClient!.Object,
            _context!,
            loggerFactory.CreateLogger<KycNotificationService>());
        var templatePersonalizer = new TemplatePersonalizer(
            loggerFactory.CreateLogger<TemplatePersonalizer>());

        var handler = new EddApprovedEventHandler(
            notificationService,
            templatePersonalizer,
            loggerFactory.CreateLogger<EddApprovedEventHandler>());

        var domainEvent = new EddApprovedEvent
        {
            ClientId = _testClientId,
            KycStatusId = Guid.NewGuid(),
            ClientName = "John Banda",
            ComplianceApprovedBy = "compliance-officer",
            CeoApprovedBy = "ceo",
            RiskAcceptanceLevel = "EnhancedMonitoring",
            ApprovedAt = DateTime.UtcNow,
            CorrelationId = "test-correlation"
        };

        // Act
        await handler.HandleAsync(domainEvent);

        // Wait for async handler
        await Task.Delay(100);

        // Assert
        _mockCommClient.Verify(
            c => c.SendNotificationAsync(It.Is<SendNotificationRequest>(
                r => r.TemplateId == "edd_approved" &&
                     r.RecipientId == _testClientId.ToString() &&
                     r.PersonalizationData.ContainsKey("RiskAcceptanceLevel"))),
            Times.Once);
    }

    #endregion

    #region EDD Rejected Event Handler Tests

    [Fact]
    public async Task EddRejectedEvent_WithConsent_SendsNotification()
    {
        // Arrange
        await CreateTestClientWithConsent();

        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var notificationService = new KycNotificationService(
            _mockCommClient!.Object,
            _context!,
            loggerFactory.CreateLogger<KycNotificationService>());
        var templatePersonalizer = new TemplatePersonalizer(
            loggerFactory.CreateLogger<TemplatePersonalizer>());

        var handler = new EddRejectedEventHandler(
            notificationService,
            templatePersonalizer,
            loggerFactory.CreateLogger<EddRejectedEventHandler>());

        var domainEvent = new EddRejectedEvent
        {
            ClientId = _testClientId,
            KycStatusId = Guid.NewGuid(),
            ClientName = "John Banda",
            RejectedBy = "compliance-officer",
            RejectionStage = "Compliance",
            RejectionReason = "High Risk",
            RejectedAt = DateTime.UtcNow,
            CorrelationId = "test-correlation"
        };

        // Act
        await handler.HandleAsync(domainEvent);

        // Wait for async handler
        await Task.Delay(100);

        // Assert
        _mockCommClient.Verify(
            c => c.SendNotificationAsync(It.Is<SendNotificationRequest>(
                r => r.TemplateId == "edd_rejected" &&
                     r.RecipientId == _testClientId.ToString())),
            Times.Once);
    }

    #endregion

    #region Template Personalizer Tests

    [Fact]
    public void TemplatePersonalizer_KycApproved_BuildsCorrectData()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var personalizer = new TemplatePersonalizer(
            loggerFactory.CreateLogger<TemplatePersonalizer>());

        // Act
        var data = personalizer.BuildKycApprovedData(
            "John Banda",
            new DateTime(2025, 10, 21),
            "Your application will proceed");

        // Assert
        data.Should().ContainKey("ClientName");
        data["ClientName"].Should().Be("John Banda");
        data.Should().ContainKey("CompletionDate");
        data["CompletionDate"].Should().Be("October 21, 2025");
        data.Should().ContainKey("NextSteps");
        data.Should().ContainKey("BranchContact");
        data.Should().ContainKey("CompanyName");
    }

    [Fact]
    public void TemplatePersonalizer_RejectionReason_Sanitized()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var personalizer = new TemplatePersonalizer(
            loggerFactory.CreateLogger<TemplatePersonalizer>());

        // Act
        var data = personalizer.BuildKycRejectedData(
            "John Banda",
            DateTime.UtcNow,
            "Sanctions", // Internal reason
            null);

        // Assert
        data.Should().ContainKey("RejectionReason");
        // Should be sanitized to customer-friendly message
        data["RejectionReason"].ToString().Should().Contain("Additional");
        data["RejectionReason"].ToString().Should().NotContain("Sanctions");
    }

    #endregion
}
