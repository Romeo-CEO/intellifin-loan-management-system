using IntelliFin.FinancialService.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace IntelliFin.FinancialService.Tests.Integration;

public class FinancialServiceIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public FinancialServiceIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    [Fact]
    public async Task HealthCheck_ReturnsHealthy()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("Healthy", content);
    }

    [Fact]
    public async Task ServiceInfo_ReturnsCorrectInfo()
    {
        // Act
        var response = await _client.GetAsync("/");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("IntelliFin.FinancialService", content);
        Assert.Contains("Consolidated Financial Service", content);
    }

    [Fact]
    public async Task GetAccountBalance_ReturnsBalance()
    {
        // Arrange
        var accountId = 1001;

        // Act
        var response = await _client.GetAsync($"/api/gl/accounts/{accountId}/balance");

        // Assert
        response.EnsureSuccessStatusCode();
        var balance = await response.Content.ReadFromJsonAsync<decimal>(_jsonOptions);
        Assert.True(balance >= 0);
    }

    [Fact]
    public async Task GetGLAccounts_ReturnsAccountList()
    {
        // Act
        var response = await _client.GetAsync("/api/gl/accounts");

        // Assert
        response.EnsureSuccessStatusCode();
        var accounts = await response.Content.ReadFromJsonAsync<List<GLAccount>>(_jsonOptions);
        Assert.NotNull(accounts);
        Assert.NotEmpty(accounts);
    }

    [Fact]
    public async Task PostJournalEntry_ValidEntry_ReturnsSuccess()
    {
        // Arrange
        var request = new CreateJournalEntryRequest
        {
            DebitAccountId = 1001,
            CreditAccountId = 2001,
            Amount = 1000.00m,
            Description = "Integration test entry",
            Reference = "INT-TEST-001"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/gl/journal-entries", request, _jsonOptions);

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<JournalEntryResult>(_jsonOptions);
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.JournalEntryId);
    }

    [Fact]
    public async Task GetCollectionsAccount_ReturnsAccount()
    {
        // Arrange
        var loanId = "LOAN-001";

        // Act
        var response = await _client.GetAsync($"/api/collections/accounts/{loanId}");

        // Assert
        response.EnsureSuccessStatusCode();
        var account = await response.Content.ReadFromJsonAsync<CollectionsAccount>(_jsonOptions);
        Assert.NotNull(account);
        Assert.Equal(loanId, account.LoanId);
    }

    [Fact]
    public async Task CalculateDPD_ReturnsCalculation()
    {
        // Arrange
        var loanId = "LOAN-001";

        // Act
        var response = await _client.GetAsync($"/api/collections/accounts/{loanId}/dpd");

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<DPDCalculationResult>(_jsonOptions);
        Assert.NotNull(result);
        Assert.Equal(loanId, result.LoanId);
        Assert.True(result.DaysPastDue >= 0);
    }

    [Fact]
    public async Task VerifyEmployee_ReturnsVerificationResult()
    {
        // Arrange
        var request = new EmployeeVerificationRequest
        {
            EmployeeId = "EMP001",
            NationalId = "123456789",
            Ministry = "Ministry of Health",
            FirstName = "John",
            LastName = "Doe"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/pmec/verify-employee", request, _jsonOptions);

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<EmployeeVerificationResult>(_jsonOptions);
        Assert.NotNull(result);
        Assert.Equal(request.EmployeeId, result.EmployeeId);
    }

    [Fact]
    public async Task ProcessPayment_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var request = new ProcessPaymentRequest
        {
            LoanId = "LOAN-001",
            Amount = 500.00m,
            PaymentMethod = PaymentMethod.MobileMoney,
            PhoneNumber = "+260971234567"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/payments/process", request, _jsonOptions);

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PaymentProcessingResult>(_jsonOptions);
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotEmpty(result.PaymentId);
    }

    [Fact]
    public async Task GetPaymentHistory_ReturnsHistory()
    {
        // Arrange
        var loanId = "LOAN-001";

        // Act
        var response = await _client.GetAsync($"/api/payments/loans/{loanId}/history");

        // Assert
        response.EnsureSuccessStatusCode();
        var payments = await response.Content.ReadFromJsonAsync<List<Payment>>(_jsonOptions);
        Assert.NotNull(payments);
    }

    [Fact]
    public async Task CheckPmecHealth_ReturnsHealthStatus()
    {
        // Act
        var response = await _client.GetAsync("/api/pmec/health");

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PmecHealthCheckResult>(_jsonOptions);
        Assert.NotNull(result);
        Assert.NotEmpty(result.Status);
    }

    [Fact]
    public async Task CheckPaymentGatewayHealth_ReturnsHealthStatus()
    {
        // Act
        var response = await _client.GetAsync("/api/payments/health");

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PaymentGatewayHealthResult>(_jsonOptions);
        Assert.NotNull(result);
        Assert.NotEmpty(result.Gateway);
        Assert.NotEmpty(result.Status);
    }

    [Fact]
    public async Task GenerateTrialBalance_ReturnsReport()
    {
        // Arrange
        var asOfDate = DateTime.UtcNow.ToString("yyyy-MM-dd");

        // Act
        var response = await _client.GetAsync($"/api/gl/reports/trial-balance?asOfDate={asOfDate}");

        // Assert
        response.EnsureSuccessStatusCode();
        var report = await response.Content.ReadFromJsonAsync<TrialBalanceReport>(_jsonOptions);
        Assert.NotNull(report);
        Assert.NotEmpty(report.Items);
    }

    [Fact]
    public async Task GenerateCollectionsReport_ReturnsReport()
    {
        // Arrange
        var reportDate = DateTime.UtcNow.ToString("yyyy-MM-dd");

        // Act
        var response = await _client.GetAsync($"/api/collections/reports?reportDate={reportDate}");

        // Assert
        response.EnsureSuccessStatusCode();
        var report = await response.Content.ReadFromJsonAsync<CollectionsReport>(_jsonOptions);
        Assert.NotNull(report);
        Assert.True(report.TotalAccounts >= 0);
    }
}
