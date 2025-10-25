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

public class ArrearsClassificationServiceTests : IDisposable
{
    private readonly CollectionsDbContext _dbContext;
    private readonly Mock<IAuditClient> _mockAuditClient;
    private readonly Mock<ILogger<ArrearsClassificationService>> _mockLogger;
    private readonly ArrearsClassificationService _service;

    public ArrearsClassificationServiceTests()
    {
        var options = new DbContextOptionsBuilder<CollectionsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new CollectionsDbContext(options);
        _mockAuditClient = new Mock<IAuditClient>();
        _mockLogger = new Mock<ILogger<ArrearsClassificationService>>();

        _service = new ArrearsClassificationService(_dbContext, _mockAuditClient.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task ClassifyLoanAsync_ShouldClassifyAsSpecialMention_When30DaysPastDue()
    {
        // Arrange
        var schedule = CreateTestScheduleWithOverdueInstallment(daysOverdue: 30);
        _dbContext.RepaymentSchedules.Add(schedule);
        await _dbContext.SaveChangesAsync();

        // Act
        await _service.ClassifyLoanAsync(schedule.LoanId);

        // Assert
        var classification = await _dbContext.ArrearsClassificationHistory
            .FirstOrDefaultAsync(h => h.LoanId == schedule.LoanId);

        classification.Should().NotBeNull();
        classification!.NewClassification.Should().Be("SpecialMention");
        classification.DaysPastDue.Should().Be(30);
        classification.ProvisionRate.Should().Be(0.00m);
        classification.IsNonAccrual.Should().BeFalse();

        _mockAuditClient.Verify(
            x => x.LogEventAsync(
                It.Is<AuditEventPayload>(p => p.Action == "LoanReclassified"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ClassifyLoanAsync_ShouldClassifyAsSubstandard_When90DaysPastDue()
    {
        // Arrange
        var schedule = CreateTestScheduleWithOverdueInstallment(daysOverdue: 90);
        _dbContext.RepaymentSchedules.Add(schedule);
        await _dbContext.SaveChangesAsync();

        // Act
        await _service.ClassifyLoanAsync(schedule.LoanId);

        // Assert
        var classification = await _dbContext.ArrearsClassificationHistory
            .FirstOrDefaultAsync(h => h.LoanId == schedule.LoanId);

        classification.Should().NotBeNull();
        classification!.NewClassification.Should().Be("Substandard");
        classification.DaysPastDue.Should().Be(90);
        classification.ProvisionRate.Should().Be(0.20m);
        classification.IsNonAccrual.Should().BeTrue();
        classification.ProvisionAmount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ClassifyLoanAsync_ShouldClassifyAsDoubtful_When180DaysPastDue()
    {
        // Arrange
        var schedule = CreateTestScheduleWithOverdueInstallment(daysOverdue: 180);
        _dbContext.RepaymentSchedules.Add(schedule);
        await _dbContext.SaveChangesAsync();

        // Act
        await _service.ClassifyLoanAsync(schedule.LoanId);

        // Assert
        var classification = await _dbContext.ArrearsClassificationHistory
            .FirstOrDefaultAsync(h => h.LoanId == schedule.LoanId);

        classification.Should().NotBeNull();
        classification!.NewClassification.Should().Be("Doubtful");
        classification.ProvisionRate.Should().Be(0.50m);
        classification.IsNonAccrual.Should().BeTrue();
    }

    [Fact]
    public async Task ClassifyLoanAsync_ShouldClassifyAsLoss_When365DaysPastDue()
    {
        // Arrange
        var schedule = CreateTestScheduleWithOverdueInstallment(daysOverdue: 365);
        _dbContext.RepaymentSchedules.Add(schedule);
        await _dbContext.SaveChangesAsync();

        // Act
        await _service.ClassifyLoanAsync(schedule.LoanId);

        // Assert
        var classification = await _dbContext.ArrearsClassificationHistory
            .FirstOrDefaultAsync(h => h.LoanId == schedule.LoanId);

        classification.Should().NotBeNull();
        classification!.NewClassification.Should().Be("Loss");
        classification.ProvisionRate.Should().Be(1.00m);
        classification.IsNonAccrual.Should().BeTrue();
        
        // Provision amount should equal outstanding balance
        classification.ProvisionAmount.Should().BeApproximately(
            classification.OutstandingBalance, 0.01m);
    }

    [Fact]
    public async Task ClassifyLoanAsync_ShouldNotCreateHistory_WhenClassificationUnchanged()
    {
        // Arrange
        var schedule = CreateTestScheduleWithOverdueInstallment(daysOverdue: 0);
        _dbContext.RepaymentSchedules.Add(schedule);
        await _dbContext.SaveChangesAsync();

        // First classification
        await _service.ClassifyLoanAsync(schedule.LoanId);
        
        var firstCount = await _dbContext.ArrearsClassificationHistory
            .CountAsync(h => h.LoanId == schedule.LoanId);

        // Act - Classify again without changes
        await _service.ClassifyLoanAsync(schedule.LoanId);

        // Assert
        var secondCount = await _dbContext.ArrearsClassificationHistory
            .CountAsync(h => h.LoanId == schedule.LoanId);

        firstCount.Should().Be(1);
        secondCount.Should().Be(1); // Should not create duplicate
    }

    [Fact]
    public async Task ClassifyLoanAsync_ShouldUpdateInstallmentStatus_WhenOverdue()
    {
        // Arrange
        var schedule = CreateTestScheduleWithOverdueInstallment(daysOverdue: 15);
        _dbContext.RepaymentSchedules.Add(schedule);
        await _dbContext.SaveChangesAsync();

        var installmentId = schedule.Installments.First().Id;

        // Act
        await _service.ClassifyLoanAsync(schedule.LoanId);

        // Assert
        var updatedInstallment = await _dbContext.Installments.FindAsync(installmentId);
        updatedInstallment.Should().NotBeNull();
        updatedInstallment!.Status.Should().Be("Overdue");
        updatedInstallment.DaysPastDue.Should().Be(15);
    }

    [Fact]
    public async Task ClassifyAllLoansAsync_ShouldClassifyMultipleLoans()
    {
        // Arrange
        var schedule1 = CreateTestScheduleWithOverdueInstallment(daysOverdue: 30);
        var schedule2 = CreateTestScheduleWithOverdueInstallment(daysOverdue: 100);
        var schedule3 = CreateTestScheduleWithOverdueInstallment(daysOverdue: 200);
        
        _dbContext.RepaymentSchedules.AddRange(schedule1, schedule2, schedule3);
        await _dbContext.SaveChangesAsync();

        // Act
        var count = await _service.ClassifyAllLoansAsync();

        // Assert
        count.Should().Be(3);

        var classifications = await _dbContext.ArrearsClassificationHistory.ToListAsync();
        classifications.Should().HaveCount(3);
        
        classifications.Should().Contain(c => c.NewClassification == "SpecialMention");
        classifications.Should().Contain(c => c.NewClassification == "Substandard");
        classifications.Should().Contain(c => c.NewClassification == "Doubtful");
    }

    [Fact]
    public async Task GetArrearsSummaryAsync_ShouldReturnCorrectCounts()
    {
        // Arrange
        var schedule1 = CreateTestScheduleWithOverdueInstallment(daysOverdue: 30);
        var schedule2 = CreateTestScheduleWithOverdueInstallment(daysOverdue: 100);
        var schedule3 = CreateTestScheduleWithOverdueInstallment(daysOverdue: 100);
        
        _dbContext.RepaymentSchedules.AddRange(schedule1, schedule2, schedule3);
        await _dbContext.SaveChangesAsync();

        await _service.ClassifyAllLoansAsync();

        // Act
        var summary = await _service.GetArrearsSummaryAsync();

        // Assert
        summary.Should().NotBeNull();
        summary["SpecialMention"].Should().Be(1);
        summary["Substandard"].Should().Be(2);
        summary["Current"].Should().Be(0);
        summary["Doubtful"].Should().Be(0);
        summary["Loss"].Should().Be(0);
    }

    private RepaymentSchedule CreateTestScheduleWithOverdueInstallment(int daysOverdue)
    {
        var schedule = new RepaymentSchedule
        {
            Id = Guid.NewGuid(),
            LoanId = Guid.NewGuid(),
            ClientId = Guid.NewGuid(),
            ProductCode = "TEST",
            PrincipalAmount = 10000m,
            InterestRate = 0.24m,
            TermMonths = 12,
            RepaymentFrequency = "Monthly",
            FirstPaymentDate = DateTime.UtcNow.AddMonths(-2),
            MaturityDate = DateTime.UtcNow.AddMonths(10),
            GeneratedAt = DateTime.UtcNow.AddMonths(-2),
            GeneratedBy = "test",
            CreatedAtUtc = DateTime.UtcNow.AddMonths(-2)
        };

        // Create one overdue installment
        var dueDate = DateTime.UtcNow.Date.AddDays(-daysOverdue);
        
        schedule.Installments.Add(new Installment
        {
            Id = Guid.NewGuid(),
            RepaymentScheduleId = schedule.Id,
            InstallmentNumber = 1,
            DueDate = dueDate,
            PrincipalDue = 800m,
            InterestDue = 200m,
            TotalDue = 1000m,
            PrincipalPaid = 0m,
            InterestPaid = 0m,
            TotalPaid = 0m,
            PrincipalBalance = 9200m,
            Status = "Pending",
            DaysPastDue = 0,
            CreatedAtUtc = DateTime.UtcNow.AddMonths(-2)
        });

        // Add a few more future installments
        for (int i = 2; i <= 3; i++)
        {
            schedule.Installments.Add(new Installment
            {
                Id = Guid.NewGuid(),
                RepaymentScheduleId = schedule.Id,
                InstallmentNumber = i,
                DueDate = DateTime.UtcNow.Date.AddMonths(i - 2),
                PrincipalDue = 800m,
                InterestDue = 200m,
                TotalDue = 1000m,
                PrincipalBalance = 10000m - (i * 800m),
                Status = "Pending",
                CreatedAtUtc = DateTime.UtcNow.AddMonths(-2)
            });
        }

        return schedule;
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
