using IntelliFin.FinancialService.Models;
using IntelliFin.FinancialService.Services;
using IntelliFin.FinancialService.Exceptions;
using IntelliFin.Shared.Audit;
using Microsoft.Extensions.Logging;
using Moq;

namespace IntelliFin.Tests.Unit.FinancialService.Audit;

public class AuditForwardingTests
{
    [Fact]
    public async Task CollectionsService_RecordPayment_ForwardsAuditEvent()
    {
        var auditClient = new Mock<IAuditClient>();
        var logger = Mock.Of<ILogger<CollectionsService>>();
        var service = new CollectionsService(logger, auditClient.Object);

        var request = new RecordPaymentRequest
        {
            LoanId = "LN-1",
            Amount = 100m,
            Method = PaymentMethod.Cash,
            PaymentDate = DateTime.UtcNow,
            ExternalReference = "TX-1"
        };

        var result = await service.RecordPaymentAsync(request);

        result.Success.Should().BeTrue();
        auditClient.Verify(client => client.LogEventAsync(
            It.Is<AuditEventPayload>(payload =>
                payload.Action == "CollectionsPaymentRecorded" &&
                payload.EntityId == result.PaymentId &&
                payload.Actor == "system"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CollectionsService_RecordPayment_WhenAuditFails_ThrowsAuditForwardingException()
    {
        var auditClient = new Mock<IAuditClient>();
        auditClient
            .Setup(client => client.LogEventAsync(It.IsAny<AuditEventPayload>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("down"));

        var logger = Mock.Of<ILogger<CollectionsService>>();
        var service = new CollectionsService(logger, auditClient.Object);

        var request = new RecordPaymentRequest
        {
            LoanId = "LN-1",
            Amount = 50m,
            Method = PaymentMethod.Cash,
            PaymentDate = DateTime.UtcNow
        };

        Func<Task> act = async () => await service.RecordPaymentAsync(request);

        await act.Should().ThrowAsync<AuditForwardingException>();
    }

    [Fact]
    public async Task PmecService_SubmitDeductions_ForwardsAuditEvent()
    {
        var auditClient = new Mock<IAuditClient>();
        var logger = Mock.Of<ILogger<PmecService>>();
        var configuration = new ConfigurationBuilder().Build();
        var service = new PmecService(logger, configuration, auditClient.Object);

        var request = new DeductionSubmissionRequest
        {
            CycleId = "CYCLE-1",
            SubmittedBy = "pmec-ops",
            Items =
            {
                new DeductionItemRequest
                {
                    EmployeeId = "EMP-1",
                    LoanId = "LN-1",
                    Amount = 100m
                }
            }
        };

        var response = await service.SubmitDeductionsAsync(request);

        response.Success.Should().BeTrue();
        auditClient.Verify(client => client.LogEventAsync(
            It.Is<AuditEventPayload>(payload =>
                payload.Action == "PmecDeductionsSubmitted" &&
                payload.Actor == "pmec-ops" &&
                payload.EntityId == response.SubmissionId),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
