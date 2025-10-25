using FluentAssertions;
using IntelliFin.Collections.Application.Services;
using IntelliFin.Collections.Infrastructure.Messaging.Consumers;
using IntelliFin.Collections.Infrastructure.Messaging.Contracts;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IntelliFin.Collections.Tests.Infrastructure.Messaging.Consumers;

public class LoanDisbursedConsumerTests
{
    private readonly Mock<IRepaymentScheduleService> _mockScheduleService;
    private readonly Mock<ILogger<LoanDisbursedConsumer>> _mockLogger;
    private readonly LoanDisbursedConsumer _consumer;

    public LoanDisbursedConsumerTests()
    {
        _mockScheduleService = new Mock<IRepaymentScheduleService>();
        _mockLogger = new Mock<ILogger<LoanDisbursedConsumer>>();
        _consumer = new LoanDisbursedConsumer(_mockScheduleService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Consume_ShouldGenerateRepaymentSchedule()
    {
        // Arrange
        var loanId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        var correlationId = Guid.NewGuid().ToString();

        var message = new LoanDisbursed
        {
            LoanId = loanId,
            ClientId = clientId,
            ProductCode = "PAYROLL",
            DisbursedAmount = 15000m,
            InterestRate = 0.28m,
            TermMonths = 12,
            DisbursementDate = DateTime.UtcNow,
            FirstPaymentDate = DateTime.UtcNow.AddDays(30),
            DisbursedBy = "finance-officer",
            CorrelationId = correlationId
        };

        var mockContext = new Mock<ConsumeContext<LoanDisbursed>>();
        mockContext.Setup(x => x.Message).Returns(message);
        mockContext.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

        _mockScheduleService
            .Setup(x => x.GenerateScheduleAsync(
                loanId, clientId, "PAYROLL", 15000m, 0.28m, 12,
                It.IsAny<DateTime>(), correlationId, "System", CancellationToken.None))
            .ReturnsAsync(scheduleId);

        // Act
        await _consumer.Consume(mockContext.Object);

        // Assert
        _mockScheduleService.Verify(
            x => x.GenerateScheduleAsync(
                loanId, clientId, "PAYROLL", 15000m, 0.28m, 12,
                message.FirstPaymentDate, correlationId, "System", CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task Consume_ShouldThrowException_WhenScheduleGenerationFails()
    {
        // Arrange
        var message = new LoanDisbursed
        {
            LoanId = Guid.NewGuid(),
            ClientId = Guid.NewGuid(),
            ProductCode = "PAYROLL",
            DisbursedAmount = 10000m,
            InterestRate = 0.24m,
            TermMonths = 12,
            DisbursementDate = DateTime.UtcNow,
            FirstPaymentDate = DateTime.UtcNow.AddDays(30),
            DisbursedBy = "finance-officer"
        };

        var mockContext = new Mock<ConsumeContext<LoanDisbursed>>();
        mockContext.Setup(x => x.Message).Returns(message);
        mockContext.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

        _mockScheduleService
            .Setup(x => x.GenerateScheduleAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<int>(),
                It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _consumer.Consume(mockContext.Object));
    }
}
