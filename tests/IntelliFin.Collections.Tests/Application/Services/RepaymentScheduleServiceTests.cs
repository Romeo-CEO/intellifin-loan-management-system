using FluentAssertions;
using IntelliFin.Collections.Application.Services;
using IntelliFin.Collections.Infrastructure.Persistence;
using IntelliFin.Shared.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IntelliFin.Collections.Tests.Application.Services;

public class RepaymentScheduleServiceTests : IDisposable
{
    private readonly CollectionsDbContext _dbContext;
    private readonly Mock<IAuditClient> _mockAuditClient;
    private readonly Mock<ILogger<RepaymentScheduleService>> _mockLogger;
    private readonly RepaymentScheduleService _service;

    public RepaymentScheduleServiceTests()
    {
        var options = new DbContextOptionsBuilder<CollectionsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new CollectionsDbContext(options);
        _mockAuditClient = new Mock<IAuditClient>();
        _mockLogger = new Mock<ILogger<RepaymentScheduleService>>();

        _service = new RepaymentScheduleService(_dbContext, _mockAuditClient.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task GenerateScheduleAsync_ShouldCreateScheduleWithCorrectInstallments()
    {
        // Arrange
        var loanId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var productCode = "PAYROLL";
        var principalAmount = 10000m;
        var interestRate = 0.24m; // 24% annual
        var termMonths = 12;
        var firstPaymentDate = new DateTime(2025, 11, 1);
        var correlationId = Guid.NewGuid().ToString();
        var generatedBy = "test-user";

        // Act
        var scheduleId = await _service.GenerateScheduleAsync(
            loanId, clientId, productCode, principalAmount, interestRate,
            termMonths, firstPaymentDate, correlationId, generatedBy);

        // Assert
        scheduleId.Should().NotBeEmpty();

        var schedule = await _dbContext.RepaymentSchedules
            .Include(s => s.Installments)
            .FirstOrDefaultAsync(s => s.Id == scheduleId);

        schedule.Should().NotBeNull();
        schedule!.LoanId.Should().Be(loanId);
        schedule.ClientId.Should().Be(clientId);
        schedule.ProductCode.Should().Be(productCode);
        schedule.PrincipalAmount.Should().Be(principalAmount);
        schedule.InterestRate.Should().Be(interestRate);
        schedule.TermMonths.Should().Be(termMonths);
        schedule.FirstPaymentDate.Should().Be(firstPaymentDate);
        schedule.GeneratedBy.Should().Be(generatedBy);
        schedule.CorrelationId.Should().Be(correlationId);

        schedule.Installments.Should().HaveCount(termMonths);

        // Verify first installment
        var firstInstallment = schedule.Installments.OrderBy(i => i.InstallmentNumber).First();
        firstInstallment.InstallmentNumber.Should().Be(1);
        firstInstallment.DueDate.Should().Be(firstPaymentDate);
        firstInstallment.Status.Should().Be("Pending");
        firstInstallment.PrincipalPaid.Should().Be(0);
        firstInstallment.InterestPaid.Should().Be(0);
        firstInstallment.DaysPastDue.Should().Be(0);

        // Verify all installments have amounts
        foreach (var installment in schedule.Installments)
        {
            installment.PrincipalDue.Should().BeGreaterThan(0);
            installment.InterestDue.Should().BeGreaterThan(0);
            installment.TotalDue.Should().Be(installment.PrincipalDue + installment.InterestDue);
        }

        // Verify total principal equals loan amount (with rounding tolerance)
        var totalPrincipal = schedule.Installments.Sum(i => i.PrincipalDue);
        totalPrincipal.Should().BeApproximately(principalAmount, 0.01m);

        // Verify audit event was logged
        _mockAuditClient.Verify(
            x => x.LogEventAsync(
                It.Is<AuditEventPayload>(p =>
                    p.Action == "RepaymentScheduleGenerated" &&
                    p.EntityType == "RepaymentSchedule" &&
                    p.EntityId == scheduleId.ToString()),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GenerateScheduleAsync_ShouldReturnExistingSchedule_WhenScheduleAlreadyExists()
    {
        // Arrange
        var loanId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var existingScheduleId = Guid.NewGuid();

        var existingSchedule = new Domain.Entities.RepaymentSchedule
        {
            Id = existingScheduleId,
            LoanId = loanId,
            ClientId = clientId,
            ProductCode = "PAYROLL",
            PrincipalAmount = 5000m,
            InterestRate = 0.24m,
            TermMonths = 6,
            RepaymentFrequency = "Monthly",
            FirstPaymentDate = DateTime.UtcNow.AddDays(30),
            MaturityDate = DateTime.UtcNow.AddMonths(6),
            GeneratedAt = DateTime.UtcNow,
            GeneratedBy = "existing-user",
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.RepaymentSchedules.Add(existingSchedule);
        await _dbContext.SaveChangesAsync();

        // Act
        var returnedScheduleId = await _service.GenerateScheduleAsync(
            loanId, clientId, "PAYROLL", 10000m, 0.24m, 12,
            DateTime.UtcNow.AddDays(30), Guid.NewGuid().ToString(), "new-user");

        // Assert
        returnedScheduleId.Should().Be(existingScheduleId);

        // Should not create a new schedule
        var scheduleCount = await _dbContext.RepaymentSchedules.CountAsync(s => s.LoanId == loanId);
        scheduleCount.Should().Be(1);
    }

    [Fact]
    public async Task GetScheduleByLoanIdAsync_ShouldReturnScheduleWithInstallments()
    {
        // Arrange
        var loanId = Guid.NewGuid();
        var clientId = Guid.NewGuid();

        var scheduleId = await _service.GenerateScheduleAsync(
            loanId, clientId, "PAYROLL", 10000m, 0.24m, 6,
            DateTime.UtcNow.AddDays(30), Guid.NewGuid().ToString(), "test-user");

        // Act
        var schedule = await _service.GetScheduleByLoanIdAsync(loanId);

        // Assert
        schedule.Should().NotBeNull();
        schedule!.Id.Should().Be(scheduleId);
        schedule.LoanId.Should().Be(loanId);
        schedule.Installments.Should().HaveCount(6);
    }

    [Fact]
    public async Task GetScheduleByLoanIdAsync_ShouldReturnNull_WhenScheduleDoesNotExist()
    {
        // Arrange
        var nonExistentLoanId = Guid.NewGuid();

        // Act
        var schedule = await _service.GetScheduleByLoanIdAsync(nonExistentLoanId);

        // Assert
        schedule.Should().BeNull();
    }

    [Fact]
    public async Task GenerateScheduleAsync_ShouldCalculateDecreasingPrincipalBalance()
    {
        // Arrange
        var loanId = Guid.NewGuid();
        var principalAmount = 12000m;

        // Act
        var scheduleId = await _service.GenerateScheduleAsync(
            loanId, Guid.NewGuid(), "PAYROLL", principalAmount, 0.24m, 12,
            DateTime.UtcNow.AddDays(30), Guid.NewGuid().ToString(), "test-user");

        var schedule = await _dbContext.RepaymentSchedules
            .Include(s => s.Installments)
            .FirstAsync(s => s.Id == scheduleId);

        // Assert
        var orderedInstallments = schedule.Installments.OrderBy(i => i.InstallmentNumber).ToList();
        
        for (int i = 0; i < orderedInstallments.Count - 1; i++)
        {
            var current = orderedInstallments[i];
            var next = orderedInstallments[i + 1];

            // Principal balance should decrease
            next.PrincipalBalance.Should().BeLessThan(current.PrincipalBalance);
        }

        // Last installment should have zero balance
        orderedInstallments.Last().PrincipalBalance.Should().Be(0);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
