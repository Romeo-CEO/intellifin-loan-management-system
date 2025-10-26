using IntelliFin.ClientManagement.Controllers.DTOs;
using IntelliFin.ClientManagement.Infrastructure.Persistence;
using IntelliFin.ClientManagement.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Testcontainers.MsSql;

namespace IntelliFin.ClientManagement.IntegrationTests.Services;

/// <summary>
/// Integration tests for ClientService with Audit logging
/// Verifies that audit events are properly logged for client operations
/// </summary>
public class ClientServiceAuditIntegrationTests : IAsyncLifetime
{
    private readonly MsSqlContainer _msSqlContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("YourStrong!Passw0rd")
        .Build();

    private ClientManagementDbContext? _context;
    private ClientService? _service;
    private Mock<IAuditService>? _mockAuditService;

    public async Task InitializeAsync()
    {
        await _msSqlContainer.StartAsync();

        var options = new DbContextOptionsBuilder<ClientManagementDbContext>()
            .UseSqlServer(_msSqlContainer.GetConnectionString())
            .Options;

        _context = new ClientManagementDbContext(options);
        await _context.Database.MigrateAsync();

        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<ClientService>();
        var versioningLogger = loggerFactory.CreateLogger<ClientVersioningService>();

        // Create mock audit service
        _mockAuditService = new Mock<IAuditService>();
        _mockAuditService
            .Setup(x => x.LogAuditEventAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>()))
            .Returns(Task.CompletedTask);

        // Create versioning service
        var versioningService = new ClientVersioningService(_context, versioningLogger);

        _service = new ClientService(_context, logger, versioningService, _mockAuditService.Object);
    }

    public async Task DisposeAsync()
    {
        if (_context != null)
            await _context.DisposeAsync();
        await _msSqlContainer.DisposeAsync();
    }

    [Fact]
    public async Task CreateClientAsync_ShouldLogAuditEvent()
    {
        // Arrange
        var request = CreateValidClientRequest();
        var userId = "test-user";

        // Act
        var result = await _service!.CreateClientAsync(request, userId);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify audit event was logged
        _mockAuditService!.Verify(
            x => x.LogAuditEventAsync(
                "ClientCreated",
                "Client",
                It.Is<string>(id => Guid.TryParse(id, out _)),
                userId,
                It.Is<object>(data => data != null)),
            Times.Once,
            "ClientCreated audit event should be logged exactly once");
    }

    [Fact]
    public async Task CreateClientAsync_AuditEvent_ShouldIncludeClientDetails()
    {
        // Arrange
        var request = CreateValidClientRequest();
        var userId = "test-user";
        object? capturedEventData = null;

        _mockAuditService!
            .Setup(x => x.LogAuditEventAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>()))
            .Callback<string, string, string, string, object?>(
                (action, entityType, entityId, actor, eventData) =>
                {
                    capturedEventData = eventData;
                })
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service!.CreateClientAsync(request, userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capturedEventData.Should().NotBeNull();

        // Verify event data contains expected fields
        var eventDataJson = System.Text.Json.JsonSerializer.Serialize(capturedEventData);
        eventDataJson.Should().Contain("Nrc");
        eventDataJson.Should().Contain("FullName");
        eventDataJson.Should().Contain("BranchId");
    }

    [Fact]
    public async Task UpdateClientAsync_ShouldLogAuditEvent()
    {
        // Arrange
        var createResult = await _service!.CreateClientAsync(CreateValidClientRequest(), "user1");
        var clientId = createResult.Value!.Id;

        // Reset the mock to clear the create call
        _mockAuditService!.Reset();
        _mockAuditService
            .Setup(x => x.LogAuditEventAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>()))
            .Returns(Task.CompletedTask);

        var updateRequest = new UpdateClientRequest
        {
            FirstName = "Jane",
            LastName = "Smith",
            MaritalStatus = "Married",
            PrimaryPhone = "+260971234567",
            PhysicalAddress = "456 New Street",
            City = "Ndola",
            Province = "Copperbelt"
        };

        var userId = "user2";

        // Act
        var result = await _service.UpdateClientAsync(clientId, updateRequest, userId);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify audit event was logged
        _mockAuditService.Verify(
            x => x.LogAuditEventAsync(
                "ClientUpdated",
                "Client",
                clientId.ToString(),
                userId,
                It.Is<object>(data => data != null)),
            Times.Once,
            "ClientUpdated audit event should be logged exactly once");
    }

    [Fact]
    public async Task UpdateClientAsync_AuditEvent_ShouldIncludeVersionNumber()
    {
        // Arrange
        var createResult = await _service!.CreateClientAsync(CreateValidClientRequest(), "user1");
        var clientId = createResult.Value!.Id;

        object? capturedEventData = null;

        _mockAuditService!.Reset();
        _mockAuditService
            .Setup(x => x.LogAuditEventAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>()))
            .Callback<string, string, string, string, object?>(
                (action, entityType, entityId, actor, eventData) =>
                {
                    capturedEventData = eventData;
                })
            .Returns(Task.CompletedTask);

        var updateRequest = new UpdateClientRequest
        {
            FirstName = "Jane",
            LastName = "Smith",
            MaritalStatus = "Single",
            PrimaryPhone = "+260971234567",
            PhysicalAddress = "Updated Address",
            City = "Lusaka",
            Province = "Lusaka"
        };

        // Act
        var result = await _service.UpdateClientAsync(clientId, updateRequest, "user2");

        // Assert
        result.IsSuccess.Should().BeTrue();
        capturedEventData.Should().NotBeNull();

        // Verify event data contains version number
        var eventDataJson = System.Text.Json.JsonSerializer.Serialize(capturedEventData);
        eventDataJson.Should().Contain("VersionNumber");
    }

    [Fact]
    public async Task CreateClientAsync_WithDuplicateNrc_ShouldNotLogAuditEvent()
    {
        // Arrange
        var request = CreateValidClientRequest();
        await _service!.CreateClientAsync(request, "user1");

        // Reset the mock
        _mockAuditService!.Reset();
        _mockAuditService
            .Setup(x => x.LogAuditEventAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>()))
            .Returns(Task.CompletedTask);

        // Act - Try to create with same NRC (should fail)
        var result = await _service.CreateClientAsync(request, "user2");

        // Assert
        result.IsFailure.Should().BeTrue();

        // Verify no audit event was logged for failed operation
        _mockAuditService.Verify(
            x => x.LogAuditEventAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>()),
            Times.Never,
            "No audit event should be logged for failed operations");
    }

    [Fact]
    public async Task UpdateClientAsync_WhenNotExists_ShouldNotLogAuditEvent()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var updateRequest = new UpdateClientRequest
        {
            FirstName = "Jane",
            LastName = "Smith",
            MaritalStatus = "Single",
            PrimaryPhone = "+260971234567",
            PhysicalAddress = "123 Test St",
            City = "Lusaka",
            Province = "Lusaka"
        };

        // Act
        var result = await _service!.UpdateClientAsync(nonExistentId, updateRequest, "test-user");

        // Assert
        result.IsFailure.Should().BeTrue();

        // Verify no audit event was logged for failed operation
        _mockAuditService!.Verify(
            x => x.LogAuditEventAsync(
                "ClientUpdated",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>()),
            Times.Never,
            "No audit event should be logged when client doesn't exist");
    }

    [Fact]
    public async Task CreateClientAsync_MultipleClients_ShouldLogSeparateAuditEvents()
    {
        // Arrange
        var request1 = CreateValidClientRequest("111111/11/1");
        var request2 = CreateValidClientRequest("222222/22/2");
        var request3 = CreateValidClientRequest("333333/33/3");

        // Act
        await _service!.CreateClientAsync(request1, "user1");
        await _service.CreateClientAsync(request2, "user2");
        await _service.CreateClientAsync(request3, "user3");

        // Assert
        _mockAuditService!.Verify(
            x => x.LogAuditEventAsync(
                "ClientCreated",
                "Client",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>()),
            Times.Exactly(3),
            "Should log one audit event per client created");
    }

    private static CreateClientRequest CreateValidClientRequest(string nrc = "123456/78/9")
    {
        return new CreateClientRequest
        {
            Nrc = nrc,
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
            Gender = "M",
            MaritalStatus = "Single",
            PrimaryPhone = "+260977123456",
            PhysicalAddress = "123 Test Street",
            City = "Lusaka",
            Province = "Lusaka",
            BranchId = Guid.NewGuid()
        };
    }
}
