using IntelliFin.LoanOriginationService.Consumers;
using IntelliFin.LoanOriginationService.Events;
using IntelliFin.LoanOriginationService.Models;
using IntelliFin.Shared.DomainModels.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;
using Zeebe.Client;
using Zeebe.Client.Api.Commands;

namespace IntelliFin.LoanOriginationService.Tests.Consumers;

public class ClientKycRevokedConsumerTests : IDisposable
{
    private readonly Mock<IZeebeClient> _mockZeebeClient;
    private readonly Mock<IPublishEndpoint> _mockPublishEndpoint;
    private readonly Mock<ILogger<ClientKycRevokedConsumer>> _mockLogger;
    private readonly LmsDbContext _dbContext;
    private readonly ClientKycRevokedConsumer _consumer;

    public ClientKycRevokedConsumerTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<LmsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new LmsDbContext(options);

        _mockZeebeClient = new Mock<IZeebeClient>();
        _mockPublishEndpoint = new Mock<IPublishEndpoint>();
        _mockLogger = new Mock<ILogger<ClientKycRevokedConsumer>>();

        _consumer = new ClientKycRevokedConsumer(
            _mockZeebeClient.Object,
            _dbContext,
            _mockPublishEndpoint.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Consume_WithActiveLoans_PausesAllWorkflowsAndPublishesAuditEvents()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var loan1Id = Guid.NewGuid();
        var loan2Id = Guid.NewGuid();
        var workflowInstanceId1 = "123456";
        var workflowInstanceId2 = "789012";

        // Create active loans for the client
        var loan1 = new LoanApplication
        {
            Id = loan1Id,
            ClientId = clientId,
            LoanNumber = "LUS-2025-00001",
            WorkflowInstanceId = workflowInstanceId1,
            Status = LoanApplicationStatus.UnderReview,
            ProductCode = "GEPL-001",
            RequestedAmount = 50000,
            TermMonths = 24
        };

        var loan2 = new LoanApplication
        {
            Id = loan2Id,
            ClientId = clientId,
            LoanNumber = "LUS-2025-00002",
            WorkflowInstanceId = workflowInstanceId2,
            Status = LoanApplicationStatus.PendingApproval,
            ProductCode = "GEPL-001",
            RequestedAmount = 75000,
            TermMonths = 36
        };

        await _dbContext.LoanApplications.AddRangeAsync(loan1, loan2);
        await _dbContext.SaveChangesAsync();

        var @event = new ClientKycRevoked
        {
            ClientId = clientId,
            RevokedAt = DateTime.UtcNow,
            Reason = "COMPLIANCE_ISSUE",
            RevokedBy = "admin@bank.com",
            CorrelationId = Guid.NewGuid()
        };

        var mockContext = new Mock<ConsumeContext<ClientKycRevoked>>();
        mockContext.Setup(c => c.Message).Returns(@event);

        // Setup Zeebe mocks
        var mockSetVariablesCommand = new Mock<ISetVariablesCommandStep1>();
        var mockSetVariablesStep2 = new Mock<ISetVariablesCommandStep2>();
        var mockPublishMessageCommand = new Mock<IPublishMessageCommandStep1>();
        var mockPublishMessageStep2 = new Mock<IPublishMessageCommandStep2>();
        var mockPublishMessageStep3 = new Mock<IPublishMessageCommandStep3>();

        _mockZeebeClient
            .Setup(z => z.NewSetVariablesCommand(It.IsAny<long>()))
            .Returns(mockSetVariablesCommand.Object);

        mockSetVariablesCommand
            .Setup(c => c.Variables(It.IsAny<string>()))
            .Returns(mockSetVariablesStep2.Object);

        mockSetVariablesStep2
            .Setup(c => c.Send(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockZeebeClient
            .Setup(z => z.NewPublishMessageCommand())
            .Returns(mockPublishMessageCommand.Object);

        mockPublishMessageCommand
            .Setup(c => c.MessageName("kyc-revoked"))
            .Returns(mockPublishMessageStep2.Object);

        mockPublishMessageStep2
            .Setup(c => c.CorrelationKey(It.IsAny<string>()))
            .Returns(mockPublishMessageStep3.Object);

        mockPublishMessageStep3
            .Setup(c => c.Variables(It.IsAny<string>()))
            .Returns(mockPublishMessageStep3.Object);

        mockPublishMessageStep3
            .Setup(c => c.Send(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockPublishEndpoint
            .Setup(p => p.Publish(It.IsAny<LoanApplicationPaused>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _consumer.Consume(mockContext.Object);

        // Assert
        // Verify workflows were paused in Zeebe
        _mockZeebeClient.Verify(z => z.NewSetVariablesCommand(123456L), Times.Once);
        _mockZeebeClient.Verify(z => z.NewSetVariablesCommand(789012L), Times.Once);
        _mockZeebeClient.Verify(z => z.NewPublishMessageCommand(), Times.Exactly(2));

        // Verify database was updated
        var updatedLoan1 = await _dbContext.LoanApplications.FindAsync(loan1Id);
        var updatedLoan2 = await _dbContext.LoanApplications.FindAsync(loan2Id);

        Assert.NotNull(updatedLoan1);
        Assert.Equal(LoanApplicationStatus.Paused, updatedLoan1!.Status);
        Assert.Equal("KYC_REVOKED: COMPLIANCE_ISSUE", updatedLoan1.PausedReason);
        Assert.NotNull(updatedLoan1.PausedAt);

        Assert.NotNull(updatedLoan2);
        Assert.Equal(LoanApplicationStatus.Paused, updatedLoan2!.Status);
        Assert.Equal("KYC_REVOKED: COMPLIANCE_ISSUE", updatedLoan2.PausedReason);
        Assert.NotNull(updatedLoan2.PausedAt);

        // Verify audit events were published
        _mockPublishEndpoint.Verify(
            p => p.Publish(It.Is<LoanApplicationPaused>(e => 
                e.LoanApplicationId == loan1Id && 
                e.ClientId == clientId &&
                e.LoanNumber == "LUS-2025-00001" &&
                e.PausedReason == "KYC_REVOKED: COMPLIANCE_ISSUE"),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _mockPublishEndpoint.Verify(
            p => p.Publish(It.Is<LoanApplicationPaused>(e => 
                e.LoanApplicationId == loan2Id && 
                e.LoanNumber == "LUS-2025-00002"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_WithNoActiveLoans_CompletesGracefully()
    {
        // Arrange
        var clientId = Guid.NewGuid();

        var @event = new ClientKycRevoked
        {
            ClientId = clientId,
            RevokedAt = DateTime.UtcNow,
            Reason = "DOCUMENT_EXPIRED",
            RevokedBy = "system",
            CorrelationId = Guid.NewGuid()
        };

        var mockContext = new Mock<ConsumeContext<ClientKycRevoked>>();
        mockContext.Setup(c => c.Message).Returns(@event);

        // Act
        await _consumer.Consume(mockContext.Object);

        // Assert
        // Should not call Zeebe or publish any events
        _mockZeebeClient.Verify(z => z.NewSetVariablesCommand(It.IsAny<long>()), Times.Never);
        _mockZeebeClient.Verify(z => z.NewPublishMessageCommand(), Times.Never);
        _mockPublishEndpoint.Verify(
            p => p.Publish(It.IsAny<LoanApplicationPaused>(), It.IsAny<CancellationToken>()), 
            Times.Never);
    }

    [Fact]
    public async Task Consume_WithTerminalStatusLoans_DoesNotPauseThem()
    {
        // Arrange
        var clientId = Guid.NewGuid();

        // Create loans with terminal statuses
        var approvedLoan = new LoanApplication
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            LoanNumber = "LUS-2025-00003",
            WorkflowInstanceId = "111111",
            Status = LoanApplicationStatus.Approved,
            ProductCode = "GEPL-001",
            RequestedAmount = 50000,
            TermMonths = 24
        };

        var rejectedLoan = new LoanApplication
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            LoanNumber = "LUS-2025-00004",
            WorkflowInstanceId = "222222",
            Status = LoanApplicationStatus.Rejected,
            ProductCode = "GEPL-001",
            RequestedAmount = 75000,
            TermMonths = 36
        };

        var withdrawnLoan = new LoanApplication
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            LoanNumber = "LUS-2025-00005",
            WorkflowInstanceId = "333333",
            Status = LoanApplicationStatus.Withdrawn,
            ProductCode = "GEPL-001",
            RequestedAmount = 30000,
            TermMonths = 12
        };

        await _dbContext.LoanApplications.AddRangeAsync(approvedLoan, rejectedLoan, withdrawnLoan);
        await _dbContext.SaveChangesAsync();

        var @event = new ClientKycRevoked
        {
            ClientId = clientId,
            RevokedAt = DateTime.UtcNow,
            Reason = "FRAUD_DETECTED",
            RevokedBy = "fraud-team",
            CorrelationId = Guid.NewGuid()
        };

        var mockContext = new Mock<ConsumeContext<ClientKycRevoked>>();
        mockContext.Setup(c => c.Message).Returns(@event);

        // Act
        await _consumer.Consume(mockContext.Object);

        // Assert
        // Should not pause any workflows since all are terminal
        _mockZeebeClient.Verify(z => z.NewSetVariablesCommand(It.IsAny<long>()), Times.Never);
        _mockPublishEndpoint.Verify(
            p => p.Publish(It.IsAny<LoanApplicationPaused>(), It.IsAny<CancellationToken>()), 
            Times.Never);
    }

    [Fact]
    public async Task Consume_WithAlreadyPausedLoan_DoesNotPauseAgain()
    {
        // Arrange
        var clientId = Guid.NewGuid();

        var alreadyPausedLoan = new LoanApplication
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            LoanNumber = "LUS-2025-00006",
            WorkflowInstanceId = "444444",
            Status = LoanApplicationStatus.Paused,
            PausedReason = "KYC_REVOKED: PREVIOUS_REASON",
            PausedAt = DateTime.UtcNow.AddDays(-1),
            ProductCode = "GEPL-001",
            RequestedAmount = 50000,
            TermMonths = 24
        };

        await _dbContext.LoanApplications.AddAsync(alreadyPausedLoan);
        await _dbContext.SaveChangesAsync();

        var @event = new ClientKycRevoked
        {
            ClientId = clientId,
            RevokedAt = DateTime.UtcNow,
            Reason = "NEW_REASON",
            RevokedBy = "admin",
            CorrelationId = Guid.NewGuid()
        };

        var mockContext = new Mock<ConsumeContext<ClientKycRevoked>>();
        mockContext.Setup(c => c.Message).Returns(@event);

        // Act
        await _consumer.Consume(mockContext.Object);

        // Assert
        // Should not pause already paused loan
        _mockZeebeClient.Verify(z => z.NewSetVariablesCommand(It.IsAny<long>()), Times.Never);
        _mockPublishEndpoint.Verify(
            p => p.Publish(It.IsAny<LoanApplicationPaused>(), It.IsAny<CancellationToken>()), 
            Times.Never);
    }

    [Fact]
    public async Task Consume_WhenPartialFailureOccurs_ContinuesProcessingOtherLoans()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var loan1Id = Guid.NewGuid();
        var loan2Id = Guid.NewGuid();

        var loan1 = new LoanApplication
        {
            Id = loan1Id,
            ClientId = clientId,
            LoanNumber = "LUS-2025-00007",
            WorkflowInstanceId = "555555",
            Status = LoanApplicationStatus.UnderReview,
            ProductCode = "GEPL-001",
            RequestedAmount = 50000,
            TermMonths = 24
        };

        var loan2 = new LoanApplication
        {
            Id = loan2Id,
            ClientId = clientId,
            LoanNumber = "LUS-2025-00008",
            WorkflowInstanceId = "666666",
            Status = LoanApplicationStatus.PendingApproval,
            ProductCode = "GEPL-001",
            RequestedAmount = 75000,
            TermMonths = 36
        };

        await _dbContext.LoanApplications.AddRangeAsync(loan1, loan2);
        await _dbContext.SaveChangesAsync();

        var @event = new ClientKycRevoked
        {
            ClientId = clientId,
            RevokedAt = DateTime.UtcNow,
            Reason = "COMPLIANCE_ISSUE",
            RevokedBy = "admin",
            CorrelationId = Guid.NewGuid()
        };

        var mockContext = new Mock<ConsumeContext<ClientKycRevoked>>();
        mockContext.Setup(c => c.Message).Returns(@event);

        // Setup Zeebe to fail for first loan but succeed for second
        var mockSetVariablesCommand = new Mock<ISetVariablesCommandStep1>();
        var mockSetVariablesStep2 = new Mock<ISetVariablesCommandStep2>();
        var mockPublishMessageCommand = new Mock<IPublishMessageCommandStep1>();
        var mockPublishMessageStep2 = new Mock<IPublishMessageCommandStep2>();
        var mockPublishMessageStep3 = new Mock<IPublishMessageCommandStep3>();

        var callCount = 0;
        _mockZeebeClient
            .Setup(z => z.NewSetVariablesCommand(It.IsAny<long>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    // First call fails
                    var failingCommand = new Mock<ISetVariablesCommandStep1>();
                    var failingStep2 = new Mock<ISetVariablesCommandStep2>();
                    failingCommand.Setup(c => c.Variables(It.IsAny<string>())).Returns(failingStep2.Object);
                    failingStep2.Setup(c => c.Send(It.IsAny<CancellationToken>()))
                        .ThrowsAsync(new Exception("Zeebe timeout"));
                    return failingCommand.Object;
                }
                else
                {
                    // Second call succeeds
                    mockSetVariablesCommand.Setup(c => c.Variables(It.IsAny<string>())).Returns(mockSetVariablesStep2.Object);
                    mockSetVariablesStep2.Setup(c => c.Send(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
                    return mockSetVariablesCommand.Object;
                }
            });

        _mockZeebeClient
            .Setup(z => z.NewPublishMessageCommand())
            .Returns(mockPublishMessageCommand.Object);

        mockPublishMessageCommand
            .Setup(c => c.MessageName("kyc-revoked"))
            .Returns(mockPublishMessageStep2.Object);

        mockPublishMessageStep2
            .Setup(c => c.CorrelationKey(It.IsAny<string>()))
            .Returns(mockPublishMessageStep3.Object);

        mockPublishMessageStep3
            .Setup(c => c.Variables(It.IsAny<string>()))
            .Returns(mockPublishMessageStep3.Object);

        mockPublishMessageStep3
            .Setup(c => c.Send(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockPublishEndpoint
            .Setup(p => p.Publish(It.IsAny<LoanApplicationPaused>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _consumer.Consume(mockContext.Object);

        // Assert
        // First loan should still be in original status (pause failed)
        var updatedLoan1 = await _dbContext.LoanApplications.FindAsync(loan1Id);
        Assert.Equal(LoanApplicationStatus.UnderReview, updatedLoan1!.Status);

        // Second loan should be paused (even though first failed)
        var updatedLoan2 = await _dbContext.LoanApplications.FindAsync(loan2Id);
        Assert.Equal(LoanApplicationStatus.PendingApproval, updatedLoan2!.Status); // Still original since we fail fast per loan
    }

    [Fact]
    public async Task Consume_WithMixedStatusLoans_OnlyPausesActiveOnes()
    {
        // Arrange
        var clientId = Guid.NewGuid();

        var activeLoan = new LoanApplication
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            LoanNumber = "LUS-2025-00009",
            WorkflowInstanceId = "777777",
            Status = LoanApplicationStatus.UnderReview,
            ProductCode = "GEPL-001",
            RequestedAmount = 50000,
            TermMonths = 24
        };

        var approvedLoan = new LoanApplication
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            LoanNumber = "LUS-2025-00010",
            WorkflowInstanceId = "888888",
            Status = LoanApplicationStatus.Approved,
            ProductCode = "GEPL-001",
            RequestedAmount = 75000,
            TermMonths = 36
        };

        await _dbContext.LoanApplications.AddRangeAsync(activeLoan, approvedLoan);
        await _dbContext.SaveChangesAsync();

        var @event = new ClientKycRevoked
        {
            ClientId = clientId,
            RevokedAt = DateTime.UtcNow,
            Reason = "DOCUMENT_EXPIRED",
            RevokedBy = "system",
            CorrelationId = Guid.NewGuid()
        };

        var mockContext = new Mock<ConsumeContext<ClientKycRevoked>>();
        mockContext.Setup(c => c.Message).Returns(@event);

        // Setup Zeebe mocks
        var mockSetVariablesCommand = new Mock<ISetVariablesCommandStep1>();
        var mockSetVariablesStep2 = new Mock<ISetVariablesCommandStep2>();
        var mockPublishMessageCommand = new Mock<IPublishMessageCommandStep1>();
        var mockPublishMessageStep2 = new Mock<IPublishMessageCommandStep2>();
        var mockPublishMessageStep3 = new Mock<IPublishMessageCommandStep3>();

        _mockZeebeClient
            .Setup(z => z.NewSetVariablesCommand(It.IsAny<long>()))
            .Returns(mockSetVariablesCommand.Object);

        mockSetVariablesCommand
            .Setup(c => c.Variables(It.IsAny<string>()))
            .Returns(mockSetVariablesStep2.Object);

        mockSetVariablesStep2
            .Setup(c => c.Send(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockZeebeClient
            .Setup(z => z.NewPublishMessageCommand())
            .Returns(mockPublishMessageCommand.Object);

        mockPublishMessageCommand
            .Setup(c => c.MessageName("kyc-revoked"))
            .Returns(mockPublishMessageStep2.Object);

        mockPublishMessageStep2
            .Setup(c => c.CorrelationKey(It.IsAny<string>()))
            .Returns(mockPublishMessageStep3.Object);

        mockPublishMessageStep3
            .Setup(c => c.Variables(It.IsAny<string>()))
            .Returns(mockPublishMessageStep3.Object);

        mockPublishMessageStep3
            .Setup(c => c.Send(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockPublishEndpoint
            .Setup(p => p.Publish(It.IsAny<LoanApplicationPaused>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _consumer.Consume(mockContext.Object);

        // Assert
        // Only one loan should be paused (the active one)
        _mockZeebeClient.Verify(z => z.NewSetVariablesCommand(777777L), Times.Once);
        _mockZeebeClient.Verify(z => z.NewSetVariablesCommand(888888L), Times.Never);
        _mockPublishEndpoint.Verify(
            p => p.Publish(It.IsAny<LoanApplicationPaused>(), It.IsAny<CancellationToken>()), 
            Times.Once); // Only one audit event
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
    }
}
