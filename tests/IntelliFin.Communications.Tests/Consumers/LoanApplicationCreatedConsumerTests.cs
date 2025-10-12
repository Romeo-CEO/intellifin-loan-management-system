using IntelliFin.Communications.Consumers;
using IntelliFin.Communications.Models;
using IntelliFin.Communications.Services;
using IntelliFin.Shared.DomainModels.Data;
using IntelliFin.Shared.DomainModels.Entities;
using IntelliFin.Shared.DomainModels.Repositories;
using IntelliFin.Shared.Infrastructure.Messaging.Contracts;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace IntelliFin.Communications.Tests.Consumers;

public class LoanApplicationCreatedConsumerTests
{
    [Fact]
    public async Task Consume_ShouldDispatchNotifications_ForHighValueApplications()
    {
        var repositoryMock = new Mock<INotificationRepository>();
        var smsMock = new Mock<ISmsService>();
        var inAppMock = new Mock<IInAppNotificationService>();

        repositoryMock
            .Setup(r => r.IsEventProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        repositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<NotificationLog>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationLog log, CancellationToken _) =>
            {
                log.Id = log.Id == 0 ? new Random().Next(1, 1_000_000) : log.Id;
                return log;
            });

        repositoryMock
            .Setup(r => r.UpdateStatusAsync(It.IsAny<long>(), It.IsAny<NotificationStatus>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        repositoryMock
            .Setup(r => r.MarkEventProcessedAsync(It.IsAny<Guid>(), It.IsAny<string>(), true, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        smsMock
            .Setup(s => s.SendSmsAsync(It.IsAny<SmsNotificationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SmsNotificationResponse
            {
                NotificationId = "sms-1",
                ProviderMessageId = "provider-1",
                Status = SmsDeliveryStatus.Sent
            });

        inAppMock
            .Setup(s => s.SendNotificationAsync(It.IsAny<CreateInAppNotificationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InAppNotificationResponse
            {
                Success = true,
                NotificationId = Guid.NewGuid().ToString()
            });

        var options = new DbContextOptionsBuilder<LmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new LmsDbContext(options);

        var consumer = new LoanApplicationCreatedConsumer(
            repositoryMock.Object,
            dbContext,
            smsMock.Object,
            inAppMock.Object,
            NullLogger<LoanApplicationCreatedConsumer>.Instance);

        var eventData = new LoanApplicationCreated(
            applicationId: Guid.NewGuid(),
            clientId: Guid.NewGuid(),
            amount: 150_000m,
            termMonths: 24,
            productCode: "PRD-001",
            createdAtUtc: DateTime.UtcNow)
        {
            EventId = Guid.NewGuid(),
            CustomerName = "John Doe",
            CustomerPhone = "+260971234567",
            CustomerEmail = "john@example.com",
            RequestedAmount = 150_000m,
            ProductType = "Working Capital Loan",
            BranchId = 10
        };

        var consumeContext = new TestConsumeContext<LoanApplicationCreated>(eventData);

        await consumer.Consume(consumeContext);

        repositoryMock.Verify(r => r.CreateAsync(It.IsAny<NotificationLog>(), It.IsAny<CancellationToken>()), Times.AtLeast(3));
        repositoryMock.Verify(r => r.MarkEventProcessedAsync(eventData.EventId, It.IsAny<string>(), true, It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        smsMock.Verify(s => s.SendSmsAsync(It.IsAny<SmsNotificationRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        inAppMock.Verify(s => s.SendNotificationAsync(It.IsAny<CreateInAppNotificationRequest>(), It.IsAny<CancellationToken>()), Times.AtLeast(2));
    }
}
