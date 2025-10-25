using FluentAssertions;
using IntelliFin.Collections.Application.Services;
using IntelliFin.Shared.Audit;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace IntelliFin.Collections.Tests.Application.Services;

public class NotificationServiceTests
{
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly HttpClient _httpClient;
    private readonly Mock<IAuditClient> _mockAuditClient;
    private readonly Mock<ILogger<NotificationService>> _mockLogger;
    private readonly NotificationService _service;

    public NotificationServiceTests()
    {
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:5002")
        };
        _mockAuditClient = new Mock<IAuditClient>();
        _mockLogger = new Mock<ILogger<NotificationService>>();

        _service = new NotificationService(_httpClient, _mockAuditClient.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task SendPaymentReminderAsync_ShouldSendNotification()
    {
        // Arrange
        var loanId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var amountDue = 1000m;
        var dueDate = DateTime.UtcNow.AddDays(3);
        var daysPastDue = 0;
        var correlationId = Guid.NewGuid().ToString();

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK
            });

        // Act
        await _service.SendPaymentReminderAsync(
            loanId, clientId, amountDue, dueDate, daysPastDue, correlationId);

        // Assert
        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Post &&
                req.RequestUri!.ToString().Contains("/api/notifications/send")),
            ItExpr.IsAny<CancellationToken>());

        _mockAuditClient.Verify(
            x => x.LogEventAsync(
                It.Is<AuditEventPayload>(p => p.Action == "PaymentReminderSent"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendPaymentConfirmationAsync_ShouldSendNotification()
    {
        // Arrange
        var loanId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var amountPaid = 1000m;
        var paymentDate = DateTime.UtcNow;
        var remainingBalance = 9000m;
        var correlationId = Guid.NewGuid().ToString();

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK
            });

        // Act
        await _service.SendPaymentConfirmationAsync(
            loanId, clientId, amountPaid, paymentDate, remainingBalance, correlationId);

        // Assert
        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());

        _mockAuditClient.Verify(
            x => x.LogEventAsync(
                It.Is<AuditEventPayload>(p => p.Action == "PaymentConfirmationSent"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendClassificationNotificationAsync_ShouldNotSendForMinorClassifications()
    {
        // Arrange
        var loanId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var correlationId = Guid.NewGuid().ToString();

        // Act
        await _service.SendClassificationNotificationAsync(
            loanId, clientId, "SpecialMention", 30, correlationId);

        // Assert - Should not send notification for SpecialMention
        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SendClassificationNotificationAsync_ShouldSendForSubstandardClassification()
    {
        // Arrange
        var loanId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var correlationId = Guid.NewGuid().ToString();

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK
            });

        // Act
        await _service.SendClassificationNotificationAsync(
            loanId, clientId, "Substandard", 90, correlationId);

        // Assert
        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());

        _mockAuditClient.Verify(
            x => x.LogEventAsync(
                It.Is<AuditEventPayload>(p => p.Action == "ClassificationNotificationSent"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendPaymentReminderAsync_ShouldNotThrow_WhenNotificationFails()
    {
        // Arrange
        var loanId = Guid.NewGuid();
        var clientId = Guid.NewGuid();

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act & Assert - Should not throw
        await _service.SendPaymentReminderAsync(
            loanId, clientId, 1000m, DateTime.UtcNow, 0, Guid.NewGuid().ToString());

        // Verify error was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
