using IntelliFin.ClientManagement.Services;
using IntelliFin.Shared.Audit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace IntelliFin.ClientManagement.IntegrationTests.Services;

/// <summary>
/// Integration tests for AuditService
/// Tests audit logging functionality with correlation ID propagation
/// </summary>
public class AuditServiceTests
{
    [Fact]
    public async Task LogAuditEventAsync_WithValidData_ShouldLogSuccessfully()
    {
        // Arrange
        var mockAuditClient = new Mock<IAuditClient>();
        var mockLogger = new Mock<ILogger<AuditService>>();
        var mockHttpContextAccessor = CreateMockHttpContextAccessor();

        var auditService = new AuditService(
            mockAuditClient.Object,
            mockLogger.Object,
            mockHttpContextAccessor.Object);

        // Act
        await auditService.LogAuditEventAsync(
            action: "ClientCreated",
            entityType: "Client",
            entityId: Guid.NewGuid().ToString(),
            actor: "test-user",
            eventData: new { TestData = "test-value" });

        // Give the fire-and-forget task time to complete
        await Task.Delay(100);

        // Assert
        mockAuditClient.Verify(
            x => x.LogEventAsync(
                It.Is<AuditEventPayload>(p =>
                    p.Action == "ClientCreated" &&
                    p.EntityType == "Client" &&
                    p.Actor == "test-user" &&
                    p.CorrelationId != null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LogAuditEventAsync_WithNullActor_ShouldDefaultToSystem()
    {
        // Arrange
        var mockAuditClient = new Mock<IAuditClient>();
        var mockLogger = new Mock<ILogger<AuditService>>();
        var mockHttpContextAccessor = CreateMockHttpContextAccessor();

        var auditService = new AuditService(
            mockAuditClient.Object,
            mockLogger.Object,
            mockHttpContextAccessor.Object);

        // Act
        await auditService.LogAuditEventAsync(
            action: "SystemAction",
            entityType: "Client",
            entityId: Guid.NewGuid().ToString(),
            actor: "",
            eventData: null);

        // Give the fire-and-forget task time to complete
        await Task.Delay(100);

        // Assert
        mockAuditClient.Verify(
            x => x.LogEventAsync(
                It.Is<AuditEventPayload>(p => p.Actor == "system"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LogAuditEventAsync_WithCorrelationIdInHeader_ShouldPropagateIt()
    {
        // Arrange
        var mockAuditClient = new Mock<IAuditClient>();
        var mockLogger = new Mock<ILogger<AuditService>>();
        var expectedCorrelationId = Guid.NewGuid().ToString();
        var mockHttpContextAccessor = CreateMockHttpContextAccessor(expectedCorrelationId);

        var auditService = new AuditService(
            mockAuditClient.Object,
            mockLogger.Object,
            mockHttpContextAccessor.Object);

        // Act
        await auditService.LogAuditEventAsync(
            action: "ClientCreated",
            entityType: "Client",
            entityId: Guid.NewGuid().ToString(),
            actor: "test-user");

        // Give the fire-and-forget task time to complete
        await Task.Delay(100);

        // Assert
        mockAuditClient.Verify(
            x => x.LogEventAsync(
                It.Is<AuditEventPayload>(p => p.CorrelationId == expectedCorrelationId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LogAuditEventAsync_WithActivityTraceId_ShouldUseIt()
    {
        // Arrange
        var mockAuditClient = new Mock<IAuditClient>();
        var mockLogger = new Mock<ILogger<AuditService>>();
        var mockHttpContextAccessor = CreateMockHttpContextAccessor();

        var auditService = new AuditService(
            mockAuditClient.Object,
            mockLogger.Object,
            mockHttpContextAccessor.Object);

        // Create an Activity to simulate OpenTelemetry tracing
        using var activity = new Activity("TestActivity").Start();
        var expectedTraceId = activity.TraceId.ToString();

        // Act
        await auditService.LogAuditEventAsync(
            action: "ClientCreated",
            entityType: "Client",
            entityId: Guid.NewGuid().ToString(),
            actor: "test-user");

        // Give the fire-and-forget task time to complete
        await Task.Delay(100);

        // Assert
        mockAuditClient.Verify(
            x => x.LogEventAsync(
                It.Is<AuditEventPayload>(p => p.CorrelationId == expectedTraceId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LogAuditEventAsync_WithClientFailure_ShouldNotThrow()
    {
        // Arrange
        var mockAuditClient = new Mock<IAuditClient>();
        mockAuditClient
            .Setup(x => x.LogEventAsync(It.IsAny<AuditEventPayload>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("AdminService unavailable"));

        var mockLogger = new Mock<ILogger<AuditService>>();
        var mockHttpContextAccessor = CreateMockHttpContextAccessor();

        var auditService = new AuditService(
            mockAuditClient.Object,
            mockLogger.Object,
            mockHttpContextAccessor.Object);

        // Act & Assert - should not throw
        await auditService.LogAuditEventAsync(
            action: "ClientCreated",
            entityType: "Client",
            entityId: Guid.NewGuid().ToString(),
            actor: "test-user");

        // Give the fire-and-forget task time to complete
        await Task.Delay(100);

        // Verify error was logged
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task LogAuditEventAsync_WithIpAddress_ShouldIncludeIt()
    {
        // Arrange
        var mockAuditClient = new Mock<IAuditClient>();
        var mockLogger = new Mock<ILogger<AuditService>>();
        var expectedIp = "192.168.1.100";
        var mockHttpContextAccessor = CreateMockHttpContextAccessor(ipAddress: expectedIp);

        var auditService = new AuditService(
            mockAuditClient.Object,
            mockLogger.Object,
            mockHttpContextAccessor.Object);

        // Act
        await auditService.LogAuditEventAsync(
            action: "ClientCreated",
            entityType: "Client",
            entityId: Guid.NewGuid().ToString(),
            actor: "test-user");

        // Give the fire-and-forget task time to complete
        await Task.Delay(100);

        // Assert
        mockAuditClient.Verify(
            x => x.LogEventAsync(
                It.Is<AuditEventPayload>(p => p.IpAddress == expectedIp),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LogAuditEventAsync_WithForwardedFor_ShouldUseForwardedIp()
    {
        // Arrange
        var mockAuditClient = new Mock<IAuditClient>();
        var mockLogger = new Mock<ILogger<AuditService>>();
        var forwardedIp = "10.0.0.5";
        var mockHttpContextAccessor = CreateMockHttpContextAccessor(
            ipAddress: "192.168.1.1",
            forwardedFor: $"{forwardedIp}, 10.0.0.6");

        var auditService = new AuditService(
            mockAuditClient.Object,
            mockLogger.Object,
            mockHttpContextAccessor.Object);

        // Act
        await auditService.LogAuditEventAsync(
            action: "ClientCreated",
            entityType: "Client",
            entityId: Guid.NewGuid().ToString(),
            actor: "test-user");

        // Give the fire-and-forget task time to complete
        await Task.Delay(100);

        // Assert - should use first IP from X-Forwarded-For
        mockAuditClient.Verify(
            x => x.LogEventAsync(
                It.Is<AuditEventPayload>(p => p.IpAddress == forwardedIp),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task FlushAsync_ShouldComplete()
    {
        // Arrange
        var mockAuditClient = new Mock<IAuditClient>();
        var mockLogger = new Mock<ILogger<AuditService>>();
        var mockHttpContextAccessor = CreateMockHttpContextAccessor();

        var auditService = new AuditService(
            mockAuditClient.Object,
            mockLogger.Object,
            mockHttpContextAccessor.Object);

        // Act & Assert - should complete without error
        await auditService.FlushAsync();
    }

    private static IHttpContextAccessor CreateMockHttpContextAccessor(
        string? correlationId = null,
        string? ipAddress = null,
        string? forwardedFor = null)
    {
        var mockHttpContext = new Mock<HttpContext>();
        var mockRequest = new Mock<HttpRequest>();
        var mockConnection = new Mock<ConnectionInfo>();

        // Setup headers
        var headers = new HeaderDictionary();
        if (correlationId != null)
        {
            headers["X-Correlation-Id"] = correlationId;
        }
        if (forwardedFor != null)
        {
            headers["X-Forwarded-For"] = forwardedFor;
        }
        headers["User-Agent"] = "TestAgent/1.0";

        mockRequest.Setup(r => r.Headers).Returns(headers);
        mockHttpContext.Setup(c => c.Request).Returns(mockRequest.Object);
        mockHttpContext.Setup(c => c.TraceIdentifier).Returns(Guid.NewGuid().ToString());

        // Setup connection info
        if (ipAddress != null)
        {
            mockConnection.Setup(c => c.RemoteIpAddress)
                .Returns(System.Net.IPAddress.Parse(ipAddress));
        }
        mockHttpContext.Setup(c => c.Connection).Returns(mockConnection.Object);

        var mockAccessor = new Mock<IHttpContextAccessor>();
        mockAccessor.Setup(a => a.HttpContext).Returns(mockHttpContext.Object);

        return mockAccessor.Object;
    }
}
