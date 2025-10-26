using FluentAssertions;
using IntelliFin.Collections.Application.Services;
using IntelliFin.Collections.Domain.Entities;
using IntelliFin.Collections.Infrastructure.Persistence;
using IntelliFin.Shared.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IntelliFin.Collections.Tests.Application.Services;

public class PaymentProcessingServiceTests : IDisposable
{
    private readonly CollectionsDbContext _dbContext;
    private readonly Mock<IAuditClient> _mockAuditClient;
    private readonly Mock<ILogger<PaymentProcessingService>> _mockLogger;
    private readonly PaymentProcessingService _service;

    public PaymentProcessingServiceTests()
    {
        var options = new DbContextOptionsBuilder<CollectionsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new CollectionsDbContext(options);
        _mockAuditClient = new Mock<IAuditClient>();
        _mockLogger = new Mock<ILogger<PaymentProcessingService>>();

        _service = new PaymentProcessingService(_dbContext, _mockAuditClient.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldAllocatePaymentToInstallments()
    {
        // Arrange
        var loanId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var schedule = CreateTestSchedule(loanId, clientId, 10000m, 0.24m, 12);
        _dbContext.RepaymentSchedules.Add(schedule);
        await _dbContext.SaveChangesAsync();

        var firstInstallment = schedule.Installments.OrderBy(i => i.InstallmentNumber).First();
        var paymentAmount = firstInstallment.TotalDue;

        // Act
        var paymentId = await _service.ProcessPaymentAsync(
            loanId: loanId,
            clientId: clientId,
            transactionReference: "PAY-001",
            paymentMethod: "BankTransfer",
            paymentSource: "BankTransfer",
            amount: paymentAmount,
            transactionDate: DateTime.UtcNow,
            externalReference: null,
            notes: "Test payment",
            createdBy: "test-user",
            correlationId: Guid.NewGuid().ToString());

        // Assert
        paymentId.Should().NotBeEmpty();

        var payment = await _dbContext.PaymentTransactions.FindAsync(paymentId);
        payment.Should().NotBeNull();
        payment!.Amount.Should().Be(paymentAmount);
        payment.Status.Should().Be("Confirmed");
        payment.LoanId.Should().Be(loanId);

        // Verify installment was paid
        var updatedInstallment = await _dbContext.Installments.FindAsync(firstInstallment.Id);
        updatedInstallment!.TotalPaid.Should().Be(paymentAmount);
        updatedInstallment.Status.Should().Be("Paid");
        updatedInstallment.PaidDate.Should().NotBeNull();

        // Verify audit event was logged
        _mockAuditClient.Verify(
            x => x.LogEventAsync(
                It.Is<AuditEventPayload>(p => p.Action == "PaymentProcessed"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldAllocateToMultipleInstallments()
    {
        // Arrange
        var loanId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var schedule = CreateTestSchedule(loanId, clientId, 10000m, 0.24m, 12);
        _dbContext.RepaymentSchedules.Add(schedule);
        await _dbContext.SaveChangesAsync();

        var installments = schedule.Installments.OrderBy(i => i.InstallmentNumber).Take(3).ToList();
        var paymentAmount = installments.Sum(i => i.TotalDue);

        // Act
        var paymentId = await _service.ProcessPaymentAsync(
            loanId: loanId,
            clientId: clientId,
            transactionReference: "PAY-002",
            paymentMethod: "Cash",
            paymentSource: "Cash",
            amount: paymentAmount,
            transactionDate: DateTime.UtcNow,
            externalReference: null,
            notes: "Test payment",
            createdBy: "test-user",
            correlationId: Guid.NewGuid().ToString());

        // Assert
        var payment = await _dbContext.PaymentTransactions.FindAsync(paymentId);
        payment.Should().NotBeNull();
        payment!.PrincipalPortion.Should().BeGreaterThan(0);
        payment.InterestPortion.Should().BeGreaterThan(0);

        // Verify all three installments were paid
        foreach (var installment in installments)
        {
            var updated = await _dbContext.Installments.FindAsync(installment.Id);
            updated!.Status.Should().Be("Paid");
            updated.TotalPaid.Should().BeApproximately(updated.TotalDue, 0.01m);
        }
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldCreateReconciliationTaskForOverpayment()
    {
        // Arrange
        var loanId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var schedule = CreateTestSchedule(loanId, clientId, 10000m, 0.24m, 12);
        _dbContext.RepaymentSchedules.Add(schedule);
        await _dbContext.SaveChangesAsync();

        var totalDue = schedule.Installments.Sum(i => i.TotalDue);
        var overpayment = 100m;
        var paymentAmount = totalDue + overpayment;

        // Act
        var paymentId = await _service.ProcessPaymentAsync(
            loanId: loanId,
            clientId: clientId,
            transactionReference: "PAY-003",
            paymentMethod: "BankTransfer",
            paymentSource: "BankTransfer",
            amount: paymentAmount,
            transactionDate: DateTime.UtcNow,
            externalReference: null,
            notes: "Overpayment test",
            createdBy: "test-user",
            correlationId: Guid.NewGuid().ToString());

        // Assert
        var reconciliationTask = await _dbContext.ReconciliationTasks
            .FirstOrDefaultAsync(t => t.PaymentTransactionId == paymentId);

        reconciliationTask.Should().NotBeNull();
        reconciliationTask!.TaskType.Should().Be("OverPayment");
        reconciliationTask.Status.Should().Be("Pending");
        reconciliationTask.Variance.Should().BeApproximately(overpayment, 0.02m);
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldReturnExistingPayment_ForDuplicateReference()
    {
        // Arrange
        var loanId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var schedule = CreateTestSchedule(loanId, clientId, 10000m, 0.24m, 12);
        _dbContext.RepaymentSchedules.Add(schedule);
        await _dbContext.SaveChangesAsync();

        var transactionReference = "PAY-DUP-001";

        // First payment
        var firstPaymentId = await _service.ProcessPaymentAsync(
            loanId: loanId,
            clientId: clientId,
            transactionReference: transactionReference,
            paymentMethod: "Cash",
            paymentSource: "Cash",
            amount: 1000m,
            transactionDate: DateTime.UtcNow,
            externalReference: null,
            notes: "First payment",
            createdBy: "test-user",
            correlationId: Guid.NewGuid().ToString());

        // Act - Try to process duplicate
        var duplicatePaymentId = await _service.ProcessPaymentAsync(
            loanId: loanId,
            clientId: clientId,
            transactionReference: transactionReference,
            paymentMethod: "Cash",
            paymentSource: "Cash",
            amount: 1000m,
            transactionDate: DateTime.UtcNow,
            externalReference: null,
            notes: "Duplicate payment",
            createdBy: "test-user",
            correlationId: Guid.NewGuid().ToString());

        // Assert
        duplicatePaymentId.Should().Be(firstPaymentId);

        var paymentCount = await _dbContext.PaymentTransactions
            .CountAsync(p => p.TransactionReference == transactionReference);
        paymentCount.Should().Be(1);
    }

    [Fact]
    public async Task ReconcilePaymentAsync_ShouldMarkPaymentAsReconciled()
    {
        // Arrange
        var loanId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var schedule = CreateTestSchedule(loanId, clientId, 10000m, 0.24m, 12);
        _dbContext.RepaymentSchedules.Add(schedule);
        await _dbContext.SaveChangesAsync();

        var paymentId = await _service.ProcessPaymentAsync(
            loanId, clientId, "PAY-REC-001", "Cash", "Cash", 1000m,
            DateTime.UtcNow, null, null, "test-user", Guid.NewGuid().ToString());

        // Act
        await _service.ReconcilePaymentAsync(paymentId, "reconciler", "Reconciled successfully");

        // Assert
        var payment = await _dbContext.PaymentTransactions.FindAsync(paymentId);
        payment!.IsReconciled.Should().BeTrue();
        payment.ReconciledBy.Should().Be("reconciler");
        payment.ReconciledAt.Should().NotBeNull();
        payment.Status.Should().Be("Reconciled");

        _mockAuditClient.Verify(
            x => x.LogEventAsync(
                It.Is<AuditEventPayload>(p => p.Action == "PaymentReconciled"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetUnreconciledPaymentsAsync_ShouldReturnOnlyUnreconciledPayments()
    {
        // Arrange
        var loanId1 = Guid.NewGuid();
        var loanId2 = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        
        var schedule1 = CreateTestSchedule(loanId1, clientId, 10000m, 0.24m, 12);
        var schedule2 = CreateTestSchedule(loanId2, clientId, 10000m, 0.24m, 12);
        _dbContext.RepaymentSchedules.AddRange(schedule1, schedule2);
        await _dbContext.SaveChangesAsync();

        var payment1Id = await _service.ProcessPaymentAsync(
            loanId1, clientId, "PAY-U1", "Cash", "Cash", 1000m,
            DateTime.UtcNow, null, null, "test-user", Guid.NewGuid().ToString());

        var payment2Id = await _service.ProcessPaymentAsync(
            loanId2, clientId, "PAY-U2", "Cash", "Cash", 1000m,
            DateTime.UtcNow, null, null, "test-user", Guid.NewGuid().ToString());

        // Reconcile first payment
        await _service.ReconcilePaymentAsync(payment1Id, "reconciler", null);

        // Act
        var unreconciledPayments = await _service.GetUnreconciledPaymentsAsync();

        // Assert
        unreconciledPayments.Should().HaveCount(1);
        unreconciledPayments.First().Id.Should().Be(payment2Id);
    }

    private RepaymentSchedule CreateTestSchedule(
        Guid loanId, Guid clientId, decimal principal, decimal rate, int months)
    {
        var schedule = new RepaymentSchedule
        {
            Id = Guid.NewGuid(),
            LoanId = loanId,
            ClientId = clientId,
            ProductCode = "TEST",
            PrincipalAmount = principal,
            InterestRate = rate,
            TermMonths = months,
            RepaymentFrequency = "Monthly",
            FirstPaymentDate = DateTime.UtcNow.AddMonths(1),
            MaturityDate = DateTime.UtcNow.AddMonths(months),
            GeneratedAt = DateTime.UtcNow,
            GeneratedBy = "test",
            CreatedAtUtc = DateTime.UtcNow
        };

        var monthlyRate = rate / 12m;
        var monthlyPayment = CalculateMonthlyPayment(principal, monthlyRate, months);
        var remainingBalance = principal;

        for (int i = 1; i <= months; i++)
        {
            var interestDue = Math.Round(remainingBalance * monthlyRate, 2);
            var principalDue = Math.Round(monthlyPayment - interestDue, 2);

            if (i == months)
            {
                principalDue = remainingBalance;
            }

            remainingBalance -= principalDue;

            schedule.Installments.Add(new Installment
            {
                Id = Guid.NewGuid(),
                RepaymentScheduleId = schedule.Id,
                InstallmentNumber = i,
                DueDate = DateTime.UtcNow.AddMonths(i),
                PrincipalDue = principalDue,
                InterestDue = interestDue,
                TotalDue = principalDue + interestDue,
                PrincipalBalance = remainingBalance,
                Status = "Pending",
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        return schedule;
    }

    private decimal CalculateMonthlyPayment(decimal principal, decimal monthlyRate, int months)
    {
        if (monthlyRate == 0) return principal / months;

        var numerator = principal * monthlyRate * (decimal)Math.Pow((double)(1 + monthlyRate), months);
        var denominator = (decimal)Math.Pow((double)(1 + monthlyRate), months) - 1;
        return Math.Round(numerator / denominator, 2);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
