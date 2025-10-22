using FluentAssertions;
using IntelliFin.ClientManagement.Consumers;
using IntelliFin.ClientManagement.Domain.Entities;
using IntelliFin.ClientManagement.Domain.Events;
using IntelliFin.ClientManagement.EventHandlers;
using IntelliFin.ClientManagement.Infrastructure.Persistence;
using IntelliFin.ClientManagement.Integration;
using IntelliFin.ClientManagement.Integration.DTOs;
using IntelliFin.ClientManagement.Services;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Testcontainers.MsSql;
using Xunit;

namespace IntelliFin.ClientManagement.IntegrationTests.Messaging;

/// <summary>
/// Integration tests for MassTransit event publishing and consumption
/// Uses MassTransit In-Memory test harness
/// </summary>
public class MassTransitIntegrationTests : IAsyncLifetime
{
    private readonly MsSqlContainer _msSqlContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("YourStrong!Passw0rd")
        .Build();

    private IServiceProvider? _serviceProvider;
    private ITestHarness? _harness;
    private ClientManagementDbContext? _context;
    private Mock<ICommunicationsClient>? _mockCommClient;
    private Guid _testClientId;

    public async Task InitializeAsync()
    {
        await _msSqlContainer.StartAsync();

        var services = new ServiceCollection();

        // Configure logging
        services.AddLogging(builder => builder.AddConsole());

        // Configure database
        var connectionString = _msSqlContainer.GetConnectionString();
        services.AddDbContext<ClientManagementDbContext>(options =>
            options.UseSqlServer(connectionString));

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

        services.AddScoped<ICommunicationsClient>(_ => _mockCommClient.Object);

        // Register services
        services.AddScoped<INotificationService, KycNotificationService>();
        services.AddScoped<TemplatePersonalizer>();

        // Register event handlers
        services.AddScoped<IDomainEventHandler<KycCompletedEvent>, KycCompletedEventHandler>();
        services.AddScoped<IDomainEventHandler<KycRejectedEvent>, KycRejectedEventHandler>();
        services.AddScoped<IDomainEventHandler<EddEscalatedEvent>, EddEscalatedEventHandler>();
        services.AddScoped<IDomainEventHandler<EddApprovedEvent>, EddApprovedEventHandler>();
        services.AddScoped<IDomainEventHandler<EddRejectedEvent>, EddRejectedEventHandler>();

        // Configure MassTransit with In-Memory test harness
        services.AddMassTransit(x =>
        {
            x.AddConsumer<KycCompletedEventConsumer>();
            x.AddConsumer<KycRejectedEventConsumer>();
            x.AddConsumer<EddEscalatedEventConsumer>();
            x.AddConsumer<EddApprovedEventConsumer>();
            x.AddConsumer<EddRejectedEventConsumer>();

            x.UsingInMemory((context, cfg) =>
            {
                cfg.ConfigureEndpoints(context);
            });
        });

        // Add test harness
        services.AddMassTransitTestHarness();

        _serviceProvider = services.BuildServiceProvider();
        _harness = _serviceProvider.GetRequiredService<ITestHarness>();

        // Start test harness
        await _harness.Start();

        // Create database
        _context = _serviceProvider.GetRequiredService<ClientManagementDbContext>();
        await _context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_harness != null)
            await _harness.Stop();

        if (_serviceProvider != null)
            await _serviceProvider.DisposeAsync();

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

    #region KYC Completed Event Tests

    [Fact]
    public async Task KycCompletedEvent_Published_ConsumedByConsumer()
    {
        // Arrange
        await CreateTestClientWithConsent();

        var evt = new KycCompletedEvent
        {
            ClientId = _testClientId,
            KycStatusId = Guid.NewGuid(),
            ClientName = "John Banda",
            CompletedAt = DateTime.UtcNow,
            CompletedBy = "system-workflow",
            RiskRating = "Low",
            RiskScore = 15,
            EddRequired = false,
            CorrelationId = Guid.NewGuid().ToString()
        };

        // Act
        await _harness!.Bus.Publish(evt);

        // Assert
        // Wait for message to be consumed
        var consumed = await _harness.Consumed.Any<KycCompletedEvent>(x => x.Context.Message.ClientId == _testClientId);
        consumed.Should().BeTrue();

        // Verify consumer processed the message
        var consumerHarness = _harness.GetConsumerHarness<KycCompletedEventConsumer>();
        var consumedMessage = await consumerHarness.Consumed.Any<KycCompletedEvent>();
        consumedMessage.Should().BeTrue();

        // Verify notification was sent
        await Task.Delay(200); // Wait for async handler
        _mockCommClient!.Verify(
            c => c.SendNotificationAsync(It.Is<SendNotificationRequest>(
                r => r.TemplateId == "kyc_approved")),
            Times.Once);
    }

    #endregion

    #region EDD Escalated Event Tests

    [Fact]
    public async Task EddEscalatedEvent_Published_ConsumedByConsumer()
    {
        // Arrange
        await CreateTestClientWithConsent();

        var evt = new EddEscalatedEvent
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
            CorrelationId = Guid.NewGuid().ToString()
        };

        // Act
        await _harness!.Bus.Publish(evt);

        // Assert
        var consumed = await _harness.Consumed.Any<EddEscalatedEvent>();
        consumed.Should().BeTrue();

        var consumerHarness = _harness.GetConsumerHarness<EddEscalatedEventConsumer>();
        var consumedMessage = await consumerHarness.Consumed.Any<EddEscalatedEvent>();
        consumedMessage.Should().BeTrue();

        // Verify notification sent
        await Task.Delay(200);
        _mockCommClient!.Verify(
            c => c.SendNotificationAsync(It.Is<SendNotificationRequest>(
                r => r.TemplateId == "kyc_edd_required")),
            Times.Once);
    }

    #endregion

    #region EDD Approved Event Tests

    [Fact]
    public async Task EddApprovedEvent_Published_ConsumedByConsumer()
    {
        // Arrange
        await CreateTestClientWithConsent();

        var evt = new EddApprovedEvent
        {
            ClientId = _testClientId,
            KycStatusId = Guid.NewGuid(),
            ClientName = "John Banda",
            ComplianceApprovedBy = "compliance-officer",
            CeoApprovedBy = "ceo",
            RiskAcceptanceLevel = "Standard",
            ApprovedAt = DateTime.UtcNow,
            CorrelationId = Guid.NewGuid().ToString()
        };

        // Act
        await _harness!.Bus.Publish(evt);

        // Assert
        var consumed = await _harness.Consumed.Any<EddApprovedEvent>();
        consumed.Should().BeTrue();

        var consumerHarness = _harness.GetConsumerHarness<EddApprovedEventConsumer>();
        var consumedMessage = await consumerHarness.Consumed.Any<EddApprovedEvent>();
        consumedMessage.Should().BeTrue();

        // Verify notification sent
        await Task.Delay(200);
        _mockCommClient!.Verify(
            c => c.SendNotificationAsync(It.Is<SendNotificationRequest>(
                r => r.TemplateId == "edd_approved")),
            Times.Once);
    }

    #endregion

    #region Multiple Events Tests

    [Fact]
    public async Task MultipleEvents_Published_AllConsumed()
    {
        // Arrange
        await CreateTestClientWithConsent();

        var evt1 = new KycCompletedEvent
        {
            ClientId = _testClientId,
            KycStatusId = Guid.NewGuid(),
            ClientName = "John Banda",
            CompletedAt = DateTime.UtcNow,
            CompletedBy = "system",
            RiskRating = "Low",
            RiskScore = 10,
            CorrelationId = Guid.NewGuid().ToString()
        };

        var evt2 = new EddApprovedEvent
        {
            ClientId = _testClientId,
            KycStatusId = Guid.NewGuid(),
            ClientName = "John Banda",
            ComplianceApprovedBy = "compliance",
            CeoApprovedBy = "ceo",
            RiskAcceptanceLevel = "Standard",
            ApprovedAt = DateTime.UtcNow,
            CorrelationId = Guid.NewGuid().ToString()
        };

        // Act
        await _harness!.Bus.Publish(evt1);
        await _harness.Bus.Publish(evt2);

        // Assert
        var consumed1 = await _harness.Consumed.Any<KycCompletedEvent>();
        var consumed2 = await _harness.Consumed.Any<EddApprovedEvent>();

        consumed1.Should().BeTrue();
        consumed2.Should().BeTrue();

        // Wait for async handlers
        await Task.Delay(300);

        // Both notifications should be sent
        _mockCommClient!.Verify(
            c => c.SendNotificationAsync(It.IsAny<SendNotificationRequest>()),
            Times.AtLeast(2));
    }

    #endregion

    #region Consumer Error Handling Tests

    [Fact]
    public async Task ConsumerException_TriggersRetry()
    {
        // Arrange
        await CreateTestClientWithConsent();

        // Setup mock to fail first 2 times, succeed on 3rd
        var attemptCount = 0;
        _mockCommClient!
            .Setup(c => c.SendNotificationAsync(It.IsAny<SendNotificationRequest>()))
            .Returns(() =>
            {
                attemptCount++;
                if (attemptCount < 3)
                {
                    throw new HttpRequestException("Transient error");
                }

                return Task.FromResult(new SendNotificationResponse
                {
                    NotificationId = Guid.NewGuid(),
                    Status = "Queued",
                    SentAt = DateTime.UtcNow,
                    Channel = "SMS"
                });
            });

        var evt = new KycCompletedEvent
        {
            ClientId = _testClientId,
            KycStatusId = Guid.NewGuid(),
            ClientName = "John Banda",
            CompletedAt = DateTime.UtcNow,
            CompletedBy = "system",
            RiskRating = "Low",
            RiskScore = 10,
            CorrelationId = Guid.NewGuid().ToString()
        };

        // Act
        await _harness!.Bus.Publish(evt);

        // Assert
        // Event should be consumed eventually
        var consumed = await _harness.Consumed.Any<KycCompletedEvent>(timeout: TimeSpan.FromSeconds(10));
        consumed.Should().BeTrue();

        // Multiple attempts should have been made
        await Task.Delay(500);
        attemptCount.Should().BeGreaterThanOrEqualTo(2);
    }

    #endregion

    #region Message Ordering Tests

    [Fact]
    public async Task EventsForSameClient_ProcessedInOrder()
    {
        // Arrange
        await CreateTestClientWithConsent();

        var processedEvents = new List<string>();
        var lockObj = new object();

        // Track event processing order
        _mockCommClient!
            .Setup(c => c.SendNotificationAsync(It.IsAny<SendNotificationRequest>()))
            .Callback<SendNotificationRequest>(req =>
            {
                lock (lockObj)
                {
                    processedEvents.Add(req.TemplateId);
                }
            })
            .ReturnsAsync(new SendNotificationResponse
            {
                NotificationId = Guid.NewGuid(),
                Status = "Queued",
                SentAt = DateTime.UtcNow,
                Channel = "SMS"
            });

        // Publish events in sequence
        await _harness!.Bus.Publish(new EddEscalatedEvent
        {
            ClientId = _testClientId,
            KycStatusId = Guid.NewGuid(),
            ClientName = "John Banda",
            EscalatedAt = DateTime.UtcNow,
            EddReason = "High Risk",
            RiskLevel = "High",
            CorrelationId = Guid.NewGuid().ToString()
        });

        await Task.Delay(50);

        await _harness.Bus.Publish(new EddApprovedEvent
        {
            ClientId = _testClientId,
            KycStatusId = Guid.NewGuid(),
            ClientName = "John Banda",
            ComplianceApprovedBy = "compliance",
            CeoApprovedBy = "ceo",
            RiskAcceptanceLevel = "Standard",
            ApprovedAt = DateTime.UtcNow,
            CorrelationId = Guid.NewGuid().ToString()
        });

        // Wait for processing
        await Task.Delay(500);

        // Assert
        processedEvents.Should().HaveCount(2);
        processedEvents[0].Should().Be("kyc_edd_required");
        processedEvents[1].Should().Be("edd_approved");
    }

    #endregion

    #region Consumer Harness Validation Tests

    [Fact]
    public async Task TestHarness_AllConsumersRegistered()
    {
        // Assert
        _harness!.GetConsumerHarness<KycCompletedEventConsumer>().Should().NotBeNull();
        _harness.GetConsumerHarness<KycRejectedEventConsumer>().Should().NotBeNull();
        _harness.GetConsumerHarness<EddEscalatedEventConsumer>().Should().NotBeNull();
        _harness.GetConsumerHarness<EddApprovedEventConsumer>().Should().NotBeNull();
        _harness.GetConsumerHarness<EddRejectedEventConsumer>().Should().NotBeNull();

        await Task.CompletedTask;
    }

    [Fact]
    public async Task TestHarness_BusStarted()
    {
        // Assert
        var busControl = _harness!.Bus;
        busControl.Should().NotBeNull();

        await Task.CompletedTask;
    }

    #endregion
}
