using IntelliFin.FinancialService.Controllers;
using IntelliFin.FinancialService.Models;
using IntelliFin.FinancialService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IntelliFin.FinancialService.Tests.Controllers;

public class GeneralLedgerControllerTests
{
    private readonly Mock<IGeneralLedgerService> _mockService;
    private readonly Mock<ILogger<GeneralLedgerController>> _mockLogger;
    private readonly GeneralLedgerController _controller;

    public GeneralLedgerControllerTests()
    {
        _mockService = new Mock<IGeneralLedgerService>();
        _mockLogger = new Mock<ILogger<GeneralLedgerController>>();
        _controller = new GeneralLedgerController(_mockService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task GetAccountBalance_ValidAccountId_ReturnsOkResult()
    {
        // Arrange
        var accountId = 1001;
        var expectedBalance = 10000.00m;
        _mockService.Setup(s => s.GetAccountBalanceAsync(accountId, null))
                   .ReturnsAsync(expectedBalance);

        // Act
        var result = await _controller.GetAccountBalance(accountId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(expectedBalance, okResult.Value);
    }

    [Fact]
    public async Task GetAccountBalance_ServiceThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        var accountId = 1001;
        _mockService.Setup(s => s.GetAccountBalanceAsync(accountId, null))
                   .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _controller.GetAccountBalance(accountId);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    [Fact]
    public async Task GetAccounts_ReturnsOkResult()
    {
        // Arrange
        var expectedAccounts = new List<GLAccount>
        {
            new GLAccount { Id = 1001, Code = "1001", Name = "Cash", Type = AccountType.Asset },
            new GLAccount { Id = 2001, Code = "2001", Name = "Accounts Payable", Type = AccountType.Liability }
        };
        _mockService.Setup(s => s.GetAccountsAsync())
                   .ReturnsAsync(expectedAccounts);

        // Act
        var result = await _controller.GetAccounts();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var accounts = Assert.IsAssignableFrom<IEnumerable<GLAccount>>(okResult.Value);
        Assert.Equal(expectedAccounts.Count, accounts.Count());
    }

    [Fact]
    public async Task GetAccount_ValidAccountId_ReturnsOkResult()
    {
        // Arrange
        var accountId = 1001;
        var expectedAccount = new GLAccount { Id = accountId, Code = "1001", Name = "Cash", Type = AccountType.Asset };
        _mockService.Setup(s => s.GetAccountAsync(accountId))
                   .ReturnsAsync(expectedAccount);

        // Act
        var result = await _controller.GetAccount(accountId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var account = Assert.IsType<GLAccount>(okResult.Value);
        Assert.Equal(accountId, account.Id);
    }

    [Fact]
    public async Task GetAccount_AccountNotFound_ReturnsNotFound()
    {
        // Arrange
        var accountId = 9999;
        _mockService.Setup(s => s.GetAccountAsync(accountId))
                   .ReturnsAsync((GLAccount?)null);

        // Act
        var result = await _controller.GetAccount(accountId);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.Contains(accountId.ToString(), notFoundResult.Value?.ToString());
    }

    [Fact]
    public async Task PostJournalEntry_ValidRequest_ReturnsOkResult()
    {
        // Arrange
        var request = new CreateJournalEntryRequest
        {
            DebitAccountId = 1001,
            CreditAccountId = 2001,
            Amount = 1000.00m,
            Description = "Test entry",
            Reference = "TEST-001"
        };
        var expectedResult = new JournalEntryResult
        {
            Success = true,
            JournalEntryId = 123,
            Message = "Journal entry posted successfully"
        };
        _mockService.Setup(s => s.PostJournalEntryAsync(request))
                   .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.PostJournalEntry(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var journalResult = Assert.IsType<JournalEntryResult>(okResult.Value);
        Assert.True(journalResult.Success);
        Assert.Equal(123, journalResult.JournalEntryId);
    }

    [Fact]
    public async Task PostJournalEntry_InvalidRequest_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateJournalEntryRequest
        {
            DebitAccountId = 1001,
            CreditAccountId = 1001, // Same account - invalid
            Amount = 1000.00m,
            Description = "Test entry",
            Reference = "TEST-001"
        };
        var expectedResult = new JournalEntryResult
        {
            Success = false,
            Message = "Validation failed",
            Errors = new List<string> { "Debit and credit accounts cannot be the same" }
        };
        _mockService.Setup(s => s.PostJournalEntryAsync(request))
                   .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.PostJournalEntry(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var journalResult = Assert.IsType<JournalEntryResult>(badRequestResult.Value);
        Assert.False(journalResult.Success);
        Assert.NotEmpty(journalResult.Errors);
    }

    [Fact]
    public async Task GenerateTrialBalance_ValidDate_ReturnsOkResult()
    {
        // Arrange
        var asOfDate = DateTime.UtcNow;
        var expectedReport = new TrialBalanceReport
        {
            AsOfDate = asOfDate,
            Items = new List<TrialBalanceItem>
            {
                new TrialBalanceItem { AccountCode = "1001", AccountName = "Cash", DebitBalance = 10000.00m }
            },
            TotalDebits = 10000.00m,
            TotalCredits = 10000.00m
        };
        _mockService.Setup(s => s.GenerateTrialBalanceAsync(asOfDate))
                   .ReturnsAsync(expectedReport);

        // Act
        var result = await _controller.GenerateTrialBalance(asOfDate);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var report = Assert.IsType<TrialBalanceReport>(okResult.Value);
        Assert.Equal(asOfDate, report.AsOfDate);
        Assert.True(report.IsBalanced);
    }

    [Fact]
    public async Task ValidateJournalEntry_ValidRequest_ReturnsTrue()
    {
        // Arrange
        var request = new CreateJournalEntryRequest
        {
            DebitAccountId = 1001,
            CreditAccountId = 2001,
            Amount = 1000.00m,
            Description = "Test entry",
            Reference = "TEST-001"
        };
        _mockService.Setup(s => s.ValidateJournalEntryAsync(request))
                   .ReturnsAsync(true);

        // Act
        var result = await _controller.ValidateJournalEntry(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.True((bool)okResult.Value!);
    }
}
