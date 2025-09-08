using IntelliFin.FinancialService.Models;
using IntelliFin.FinancialService.Services;
using IntelliFin.Shared.DomainModels.Repositories;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IntelliFin.FinancialService.Tests.Services;

public class GeneralLedgerServiceTests
{
    private readonly Mock<ILogger<GeneralLedgerService>> _mockLogger;
    private readonly Mock<IGLAccountRepository> _mockAccountRepository;
    private readonly Mock<IGLEntryRepository> _mockEntryRepository;
    private readonly GeneralLedgerService _service;

    public GeneralLedgerServiceTests()
    {
        _mockLogger = new Mock<ILogger<GeneralLedgerService>>();
        _mockAccountRepository = new Mock<IGLAccountRepository>();
        _mockEntryRepository = new Mock<IGLEntryRepository>();
        _service = new GeneralLedgerService(_mockLogger.Object, _mockAccountRepository.Object, _mockEntryRepository.Object);
    }

    [Fact]
    public async Task GetAccountBalanceAsync_ValidAccountId_ReturnsBalance()
    {
        // Arrange
        var accountId = 1001;
        var asOfDate = DateTime.UtcNow;

        // Act
        var result = await _service.GetAccountBalanceAsync(accountId, asOfDate);

        // Assert
        Assert.True(result >= 0);
        Assert.IsType<decimal>(result);
    }

    [Fact]
    public async Task PostJournalEntryAsync_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var request = new CreateJournalEntryRequest
        {
            DebitAccountId = 1001,
            CreditAccountId = 2001,
            Amount = 1000.00m,
            Description = "Test journal entry",
            Reference = "TEST-001"
        };

        // Act
        var result = await _service.PostJournalEntryAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.JournalEntryId);
        Assert.True(result.JournalEntryId > 0);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task PostJournalEntryAsync_InvalidAmount_ReturnsFailure()
    {
        // Arrange
        var request = new CreateJournalEntryRequest
        {
            DebitAccountId = 1001,
            CreditAccountId = 2001,
            Amount = -100.00m, // Invalid negative amount
            Description = "Test journal entry",
            Reference = "TEST-001"
        };

        // Act
        var result = await _service.PostJournalEntryAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.JournalEntryId);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public async Task PostJournalEntryAsync_SameDebitCreditAccount_ReturnsFailure()
    {
        // Arrange
        var request = new CreateJournalEntryRequest
        {
            DebitAccountId = 1001,
            CreditAccountId = 1001, // Same as debit account
            Amount = 1000.00m,
            Description = "Test journal entry",
            Reference = "TEST-001"
        };

        // Act
        var result = await _service.PostJournalEntryAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.JournalEntryId);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public async Task GetAccountsAsync_ReturnsAccountList()
    {
        // Act
        var result = await _service.GetAccountsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.All(result, account => 
        {
            Assert.True(account.Id > 0);
            Assert.NotEmpty(account.Code);
            Assert.NotEmpty(account.Name);
        });
    }

    [Fact]
    public async Task GetAccountAsync_ValidAccountId_ReturnsAccount()
    {
        // Arrange
        var accountId = 1001;

        // Act
        var result = await _service.GetAccountAsync(accountId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(accountId, result.Id);
        Assert.NotEmpty(result.Code);
        Assert.NotEmpty(result.Name);
    }

    [Fact]
    public async Task GenerateTrialBalanceAsync_ValidDate_ReturnsBalancedReport()
    {
        // Arrange
        var asOfDate = DateTime.UtcNow;

        // Act
        var result = await _service.GenerateTrialBalanceAsync(asOfDate);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(asOfDate, result.AsOfDate);
        Assert.NotEmpty(result.Items);
        Assert.True(result.IsBalanced);
        Assert.Equal(result.TotalDebits, result.TotalCredits);
    }

    [Fact]
    public async Task GenerateBoZReportAsync_ValidDate_ReturnsReport()
    {
        // Arrange
        var reportDate = DateTime.UtcNow;

        // Act
        var result = await _service.GenerateBoZReportAsync(reportDate);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(reportDate, result.ReportDate);
        Assert.NotEmpty(result.ReportType);
        Assert.NotEmpty(result.Balances);
    }

    [Theory]
    [InlineData(1001, 2001, 1000.00, "Valid entry", "REF-001", true)]
    [InlineData(1001, 1001, 1000.00, "Same accounts", "REF-002", false)]
    [InlineData(1001, 2001, -100.00, "Negative amount", "REF-003", false)]
    [InlineData(1001, 2001, 1000.00, "", "REF-004", false)]
    public async Task ValidateJournalEntryAsync_VariousInputs_ReturnsExpectedResult(
        int debitAccountId, int creditAccountId, decimal amount, string description, string reference, bool expectedValid)
    {
        // Arrange
        var request = new CreateJournalEntryRequest
        {
            DebitAccountId = debitAccountId,
            CreditAccountId = creditAccountId,
            Amount = amount,
            Description = description,
            Reference = reference
        };

        // Act
        var result = await _service.ValidateJournalEntryAsync(request);

        // Assert
        Assert.Equal(expectedValid, result);
    }
}
