using FluentAssertions;
using IntelliFin.ClientManagement.Domain.Entities;
using IntelliFin.ClientManagement.Infrastructure.Persistence;
using IntelliFin.ClientManagement.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Testcontainers.MsSql;
using Xunit;

namespace IntelliFin.ClientManagement.IntegrationTests.Services;

/// <summary>
/// Integration tests for enhanced AML screening with fuzzy matching
/// Tests sanctions/PEP screening with confidence scoring
/// </summary>
public class EnhancedAmlScreeningTests : IAsyncLifetime
{
    private readonly MsSqlContainer _msSqlContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("YourStrong!Passw0rd")
        .Build();

    private ClientManagementDbContext? _context;
    private ManualAmlScreeningService? _amlService;
    private Guid _testClientId;
    private Guid _testKycStatusId;

    public async Task InitializeAsync()
    {
        await _msSqlContainer.StartAsync();

        var options = new DbContextOptionsBuilder<ClientManagementDbContext>()
            .UseSqlServer(_msSqlContainer.GetConnectionString())
            .Options;

        _context = new ClientManagementDbContext(options);
        await _context.Database.MigrateAsync();

        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var fuzzyMatcher = new FuzzyNameMatcher(loggerFactory.CreateLogger<FuzzyNameMatcher>());
        _amlService = new ManualAmlScreeningService(
            _context,
            loggerFactory.CreateLogger<ManualAmlScreeningService>(),
            fuzzyMatcher);
    }

    public async Task DisposeAsync()
    {
        if (_context != null)
            await _context.DisposeAsync();
        await _msSqlContainer.DisposeAsync();
    }

    private async Task<Guid> CreateTestClient(string firstName, string lastName)
    {
        var client = new Client
        {
            Id = Guid.NewGuid(),
            Nrc = $"{Random.Shared.Next(100000, 999999)}/11/1",
            FirstName = firstName,
            LastName = lastName,
            DateOfBirth = new DateTime(1980, 1, 1),
            Gender = "M",
            MaritalStatus = "Single",
            PrimaryPhone = "+260977111111",
            PhysicalAddress = "123 Test St",
            City = "Lusaka",
            Province = "Lusaka",
            BranchId = Guid.NewGuid(),
            CreatedBy = "test-user",
            UpdatedBy = "test-user"
        };

        _context!.Clients.Add(client);

        var kycStatus = new KycStatus
        {
            Id = Guid.NewGuid(),
            ClientId = client.Id,
            CurrentState = Domain.Enums.KycState.InProgress,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.KycStatuses.Add(kycStatus);
        await _context.SaveChangesAsync();

        _testClientId = client.Id;
        _testKycStatusId = kycStatus.Id;

        return client.Id;
    }

    #region Sanctions Screening Tests

    [Fact]
    public async Task PerformSanctionsScreening_ExactMatch_ReturnsHighRiskMatch()
    {
        // Arrange
        await CreateTestClient("Vladimir", "Putin");

        // Act
        var result = await _amlService!.PerformSanctionsScreeningAsync(
            _testKycStatusId, "VLADIMIR PUTIN", "test-screener");

        // Assert
        result.IsMatch.Should().BeTrue();
        result.RiskLevel.Should().Be("High");
        result.MatchDetails.Should().NotBeNullOrEmpty();
        result.MatchDetails.Should().Contain("Vladimir");
        result.MatchDetails.Should().Contain("Putin");
    }

    [Fact]
    public async Task PerformSanctionsScreening_FuzzyMatch_ReturnsMatch()
    {
        // Arrange - Typo in first name
        await CreateTestClient("Vladmir", "Putin"); // Missing 'i'

        // Act
        var result = await _amlService!.PerformSanctionsScreeningAsync(
            _testKycStatusId, "VLADMIR PUTIN", "test-screener");

        // Assert - Should still match with high confidence
        result.IsMatch.Should().BeTrue();
        result.RiskLevel.Should().BeOneOf("High", "Medium");
    }

    [Fact]
    public async Task PerformSanctionsScreening_AliasMatch_ReturnsMatch()
    {
        // Arrange
        await CreateTestClient("Nicolas", "Maduro");

        // Act - Using alias from sanctions list
        var result = await _amlService!.PerformSanctionsScreeningAsync(
            _testKycStatusId, "NICOLAS MADURO MOROS", "test-screener");

        // Assert
        result.IsMatch.Should().BeTrue();
        result.MatchDetails.Should().Contain("Maduro");
    }

    [Fact]
    public async Task PerformSanctionsScreening_CleanClient_ReturnsNoMatch()
    {
        // Arrange
        await CreateTestClient("John", "Banda");

        // Act
        var result = await _amlService!.PerformSanctionsScreeningAsync(
            _testKycStatusId, "JOHN BANDA", "test-screener");

        // Assert
        result.IsMatch.Should().BeFalse();
        result.RiskLevel.Should().Be("Clear");
        result.MatchDetails.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task PerformSanctionsScreening_TestSanctionedPerson_ReturnsMatch()
    {
        // Arrange
        await CreateTestClient("Sanctioned", "Person");

        // Act
        var result = await _amlService!.PerformSanctionsScreeningAsync(
            _testKycStatusId, "SANCTIONED PERSON", "test-screener");

        // Assert
        result.IsMatch.Should().BeTrue();
        result.RiskLevel.Should().Be("High");
        result.MatchDetails.Should().Contain("Sanctioned");
    }

    #endregion

    #region PEP Screening Tests

    [Fact]
    public async Task PerformPepScreening_PresidentMatch_ReturnsHighRiskMatch()
    {
        // Arrange
        await CreateTestClient("Hakainde", "Hichilema");

        // Act
        var result = await _amlService!.PerformPepScreeningAsync(
            _testKycStatusId, "HAKAINDE HICHILEMA", "test-screener");

        // Assert
        result.IsMatch.Should().BeTrue();
        result.RiskLevel.Should().Be("High");
        result.MatchDetails.Should().Contain("President");
    }

    [Fact]
    public async Task PerformPepScreening_VicePresidentMatch_ReturnsHighRiskMatch()
    {
        // Arrange
        await CreateTestClient("Mutale", "Nalumango");

        // Act
        var result = await _amlService!.PerformPepScreeningAsync(
            _testKycStatusId, "MUTALE NALUMANGO", "test-screener");

        // Assert
        result.IsMatch.Should().BeTrue();
        result.RiskLevel.Should().Be("High");
        result.MatchDetails.Should().Contain("Vice President");
    }

    [Fact]
    public async Task PerformPepScreening_MinisterMatch_ReturnsHighRiskMatch()
    {
        // Arrange
        await CreateTestClient("Situmbeko", "Musokotwane");

        // Act
        var result = await _amlService!.PerformPepScreeningAsync(
            _testKycStatusId, "SITUMBEKO MUSOKOTWANE", "test-screener");

        // Assert
        result.IsMatch.Should().BeTrue();
        result.RiskLevel.Should().Be("High");
        result.MatchDetails.Should().Contain("Finance");
    }

    [Fact]
    public async Task PerformPepScreening_TestPoliticalFigure_ReturnsMatch()
    {
        // Arrange
        await CreateTestClient("Political", "Figure");

        // Act
        var result = await _amlService!.PerformPepScreeningAsync(
            _testKycStatusId, "POLITICAL FIGURE", "test-screener");

        // Assert
        result.IsMatch.Should().BeTrue();
        result.RiskLevel.Should().Be("High");
    }

    [Fact]
    public async Task PerformPepScreening_TestGovernmentOfficial_ReturnsMatch()
    {
        // Arrange
        await CreateTestClient("Government", "Official");

        // Act
        var result = await _amlService!.PerformPepScreeningAsync(
            _testKycStatusId, "GOVERNMENT OFFICIAL", "test-screener");

        // Assert
        result.IsMatch.Should().BeTrue();
        result.RiskLevel.Should().Be("Medium"); // Medium risk for this test entry
    }

    [Fact]
    public async Task PerformPepScreening_CleanClient_ReturnsNoMatch()
    {
        // Arrange
        await CreateTestClient("John", "Banda");

        // Act
        var result = await _amlService!.PerformPepScreeningAsync(
            _testKycStatusId, "JOHN BANDA", "test-screener");

        // Assert
        result.IsMatch.Should().BeFalse();
        result.RiskLevel.Should().Be("Clear");
        result.MatchDetails.Should().BeNullOrEmpty();
    }

    #endregion

    #region Complete AML Screening Tests

    [Fact]
    public async Task PerformScreening_SanctionsHit_ReturnsHighRisk()
    {
        // Arrange
        var clientId = await CreateTestClient("Vladimir", "Putin");

        // Act
        var result = await _amlService!.PerformScreeningAsync(
            clientId, _testKycStatusId, "test-screener");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.OverallRiskLevel.Should().Be("High");
        result.Value.SanctionsHit.Should().BeTrue();
        result.Value.Screenings.Should().HaveCount(2); // Sanctions + PEP

        // Verify saved to database
        var screenings = await _context!.AmlScreenings
            .Where(s => s.KycStatusId == _testKycStatusId)
            .ToListAsync();

        screenings.Should().HaveCount(2);
        screenings.Should().Contain(s => s.ScreeningType == "Sanctions" && s.IsMatch);
    }

    [Fact]
    public async Task PerformScreening_PepHit_ReturnsHighRisk()
    {
        // Arrange
        var clientId = await CreateTestClient("Hakainde", "Hichilema");

        // Act
        var result = await _amlService!.PerformScreeningAsync(
            clientId, _testKycStatusId, "test-screener");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.OverallRiskLevel.Should().Be("High");
        result.Value.PepMatch.Should().BeTrue();
        result.Value.SanctionsHit.Should().BeFalse();

        // Verify workflow variables
        result.Value.Variables.Should().ContainKey("amlRiskLevel");
        result.Value.Variables["amlRiskLevel"].Should().Be("High");
        result.Value.Variables["pepMatch"].Should().Be(true);
        result.Value.Variables["sanctionsHit"].Should().Be(false);
        result.Value.Variables["amlScreeningComplete"].Should().Be(true);
    }

    [Fact]
    public async Task PerformScreening_CleanClient_ReturnsClearRisk()
    {
        // Arrange
        var clientId = await CreateTestClient("John", "Banda");

        // Act
        var result = await _amlService!.PerformScreeningAsync(
            clientId, _testKycStatusId, "test-screener");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.OverallRiskLevel.Should().Be("Clear");
        result.Value.SanctionsHit.Should().BeFalse();
        result.Value.PepMatch.Should().BeFalse();

        // Verify screenings persisted
        var screenings = await _context!.AmlScreenings
            .Where(s => s.KycStatusId == _testKycStatusId)
            .ToListAsync();

        screenings.Should().HaveCount(2);
        screenings.Should().AllSatisfy(s => s.IsMatch.Should().BeFalse());
        screenings.Should().AllSatisfy(s => s.RiskLevel.Should().Be("Clear"));
    }

    [Fact]
    public async Task PerformScreening_InvalidClientId_ReturnsFailure()
    {
        // Arrange
        var invalidClientId = Guid.NewGuid();

        // Act
        var result = await _amlService!.PerformScreeningAsync(
            invalidClientId, _testKycStatusId, "test-screener");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    #endregion

    #region Fuzzy Matching Integration Tests

    [Theory]
    [InlineData("Kim", "Jong Un", true)] // Should match Kim Jong Un
    [InlineData("Bashar", "al-Assad", true)] // Should match Bashar al-Assad
    [InlineData("John", "Doe Sanctioned", true)] // Should match test entry
    [InlineData("Mary", "Smith", false)] // Should not match anyone
    public async Task PerformSanctionsScreening_VariousNames_CorrectMatchBehavior(
        string firstName, string lastName, bool expectedMatch)
    {
        // Arrange
        await CreateTestClient(firstName, lastName);
        var fullName = $"{firstName} {lastName}".ToUpperInvariant();

        // Act
        var result = await _amlService!.PerformSanctionsScreeningAsync(
            _testKycStatusId, fullName, "test-screener");

        // Assert
        result.IsMatch.Should().Be(expectedMatch);
    }

    [Fact]
    public async Task PerformPepScreening_NameWithMiddleName_MatchesCorrectly()
    {
        // Arrange
        await CreateTestClient("Hakainde", "Sammy Hichilema");

        // Act
        var result = await _amlService!.PerformPepScreeningAsync(
            _testKycStatusId, "HAKAINDE SAMMY HICHILEMA", "test-screener");

        // Assert - Should match due to fuzzy matching
        result.IsMatch.Should().BeTrue();
    }

    #endregion

    #region Confidence and Provider Tracking

    [Fact]
    public async Task PerformSanctionsScreening_TracksProviderVersion()
    {
        // Arrange
        await CreateTestClient("Sanctioned", "Person");

        // Act
        var result = await _amlService!.PerformSanctionsScreeningAsync(
            _testKycStatusId, "SANCTIONED PERSON", "test-screener");

        // Assert
        result.ScreeningProvider.Should().Be("Manual_v2_Fuzzy");
    }

    [Fact]
    public async Task PerformPepScreening_TracksProviderVersion()
    {
        // Arrange
        await CreateTestClient("Political", "Figure");

        // Act
        var result = await _amlService!.PerformPepScreeningAsync(
            _testKycStatusId, "POLITICAL FIGURE", "test-screener");

        // Assert
        result.ScreeningProvider.Should().Be("Manual_v2_Fuzzy_Zambia");
    }

    [Fact]
    public async Task PerformSanctionsScreening_MatchDetails_ContainsConfidenceScore()
    {
        // Arrange
        await CreateTestClient("Vladimir", "Putin");

        // Act
        var result = await _amlService!.PerformSanctionsScreeningAsync(
            _testKycStatusId, "VLADIMIR PUTIN", "test-screener");

        // Assert
        result.MatchDetails.Should().Contain("matchConfidence");
        result.MatchDetails.Should().Contain("matchType");
        result.MatchDetails.Should().Contain("levenshteinDistance");
        result.MatchDetails.Should().Contain("soundexMatch");
    }

    #endregion
}
