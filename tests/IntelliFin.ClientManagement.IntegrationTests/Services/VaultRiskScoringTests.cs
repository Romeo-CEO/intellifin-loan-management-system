using FluentAssertions;
using IntelliFin.ClientManagement.Domain.Entities;
using IntelliFin.ClientManagement.Domain.Enums;
using IntelliFin.ClientManagement.Infrastructure.Configuration;
using IntelliFin.ClientManagement.Infrastructure.Persistence;
using IntelliFin.ClientManagement.Infrastructure.VaultClient;
using IntelliFin.ClientManagement.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Testcontainers.MsSql;
using Xunit;

namespace IntelliFin.ClientManagement.IntegrationTests.Services;

/// <summary>
/// Integration tests for Vault-based risk scoring
/// Tests risk calculation, input factors, and profile management
/// </summary>
public class VaultRiskScoringTests : IAsyncLifetime
{
    private readonly MsSqlContainer _msSqlContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("YourStrong!Passw0rd")
        .Build();

    private ClientManagementDbContext? _context;
    private VaultRiskScoringService? _riskScoringService;
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

        // Setup Vault config provider with fallback config (no actual Vault needed for basic tests)
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var vaultOptions = Options.Create(new VaultOptions { Enabled = false, UseFallbackOnError = true });
        var configProvider = new VaultRiskConfigProvider(
            loggerFactory.CreateLogger<VaultRiskConfigProvider>(),
            vaultOptions);

        var rulesEngine = new RulesExecutionEngine(
            loggerFactory.CreateLogger<RulesExecutionEngine>());

        _riskScoringService = new VaultRiskScoringService(
            _context,
            configProvider,
            rulesEngine,
            loggerFactory.CreateLogger<VaultRiskScoringService>());
    }

    public async Task DisposeAsync()
    {
        if (_context != null)
            await _context.DisposeAsync();
        await _msSqlContainer.DisposeAsync();
    }

    private async Task CreateTestClient(
        bool kycComplete = true,
        bool hasSanctionsHit = false,
        bool isPep = false,
        int age = 30)
    {
        // Create client
        var client = new Client
        {
            Id = Guid.NewGuid(),
            Nrc = "111111/11/1",
            FirstName = "John",
            LastName = "Banda",
            DateOfBirth = DateTime.UtcNow.AddYears(-age),
            Gender = "M",
            MaritalStatus = "Single",
            PrimaryPhone = "+260977111111",
            PhysicalAddress = "123 Test St",
            City = "Lusaka",
            Province = "Lusaka",
            Employer = "ABC Corp",
            SourceOfFunds = "Salary",
            BranchId = Guid.NewGuid(),
            CreatedBy = "test-user",
            UpdatedBy = "test-user"
        };

        _context!.Clients.Add(client);
        _testClientId = client.Id;

        // Create KYC status
        var kycStatus = new KycStatus
        {
            Id = Guid.NewGuid(),
            ClientId = client.Id,
            CurrentState = KycState.InProgress,
            HasNrc = kycComplete,
            HasProofOfAddress = kycComplete,
            HasPayslip = kycComplete,
            HasEmploymentLetter = false,
            AmlScreeningComplete = true,
            AmlScreenedAt = DateTime.UtcNow,
            AmlScreenedBy = "system",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.KycStatuses.Add(kycStatus);
        _testKycStatusId = kycStatus.Id;

        // Create AML screenings
        if (hasSanctionsHit)
        {
            var sanctionsScreening = new AmlScreening
            {
                Id = Guid.NewGuid(),
                KycStatusId = kycStatus.Id,
                ScreeningType = "Sanctions",
                ScreeningProvider = "Manual_v2_Fuzzy",
                ScreenedAt = DateTime.UtcNow,
                ScreenedBy = "system",
                IsMatch = true,
                RiskLevel = "High",
                MatchDetails = "{\"match\":\"Sanctioned Person\"}",
                CreatedAt = DateTime.UtcNow
            };
            _context.AmlScreenings.Add(sanctionsScreening);
        }
        else
        {
            var sanctionsScreening = new AmlScreening
            {
                Id = Guid.NewGuid(),
                KycStatusId = kycStatus.Id,
                ScreeningType = "Sanctions",
                ScreeningProvider = "Manual_v2_Fuzzy",
                ScreenedAt = DateTime.UtcNow,
                ScreenedBy = "system",
                IsMatch = false,
                RiskLevel = "Clear",
                CreatedAt = DateTime.UtcNow
            };
            _context.AmlScreenings.Add(sanctionsScreening);
        }

        if (isPep)
        {
            var pepScreening = new AmlScreening
            {
                Id = Guid.NewGuid(),
                KycStatusId = kycStatus.Id,
                ScreeningType = "PEP",
                ScreeningProvider = "Manual_v2_Fuzzy_Zambia",
                ScreenedAt = DateTime.UtcNow,
                ScreenedBy = "system",
                IsMatch = true,
                RiskLevel = "High",
                MatchDetails = "{\"match\":\"Political Figure\"}",
                CreatedAt = DateTime.UtcNow
            };
            _context.AmlScreenings.Add(pepScreening);
        }
        else
        {
            var pepScreening = new AmlScreening
            {
                Id = Guid.NewGuid(),
                KycStatusId = kycStatus.Id,
                ScreeningType = "PEP",
                ScreeningProvider = "Manual_v2_Fuzzy_Zambia",
                ScreenedAt = DateTime.UtcNow,
                ScreenedBy = "system",
                IsMatch = false,
                RiskLevel = "Clear",
                CreatedAt = DateTime.UtcNow
            };
            _context.AmlScreenings.Add(pepScreening);
        }

        await _context.SaveChangesAsync();
    }

    #region Input Factors Tests

    [Fact]
    public async Task BuildInputFactors_ValidClient_ReturnsSuccess()
    {
        // Arrange
        await CreateTestClient(kycComplete: true);

        // Act
        var result = await _riskScoringService!.BuildInputFactorsAsync(_testClientId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.KycComplete.Should().BeTrue();
        result.Value.AmlScreeningComplete.Should().BeTrue();
    }

    [Fact]
    public async Task BuildInputFactors_IncompleteKyc_ReturnsCorrectFactors()
    {
        // Arrange
        await CreateTestClient(kycComplete: false);

        // Act
        var result = await _riskScoringService!.BuildInputFactorsAsync(_testClientId);

        // Assert
        result.Value!.KycComplete.Should().BeFalse();
        result.Value.AllDocumentsVerified.Should().BeFalse();
    }

    [Fact]
    public async Task BuildInputFactors_SanctionsHit_ReturnsCorrectFactors()
    {
        // Arrange
        await CreateTestClient(hasSanctionsHit: true);

        // Act
        var result = await _riskScoringService!.BuildInputFactorsAsync(_testClientId);

        // Assert
        result.Value!.HasSanctionsHit.Should().BeTrue();
        result.Value.AmlRiskLevel.Should().Be("High");
    }

    [Fact]
    public async Task BuildInputFactors_PepMatch_ReturnsCorrectFactors()
    {
        // Arrange
        await CreateTestClient(isPep: true);

        // Act
        var result = await _riskScoringService!.BuildInputFactorsAsync(_testClientId);

        // Assert
        result.Value!.IsPep.Should().BeTrue();
        result.Value.AmlRiskLevel.Should().Be("High");
    }

    [Fact]
    public async Task BuildInputFactors_YoungClient_ReturnsCorrectAge()
    {
        // Arrange
        await CreateTestClient(age: 23);

        // Act
        var result = await _riskScoringService!.BuildInputFactorsAsync(_testClientId);

        // Assert
        result.Value!.Age.Should().Be(23);
    }

    #endregion

    #region Risk Computation Tests

    [Fact]
    public async Task ComputeRisk_LowRiskClient_ReturnsLowRating()
    {
        // Arrange - Clean client with complete KYC
        await CreateTestClient(kycComplete: true, hasSanctionsHit: false, isPep: false, age: 30);

        // Act
        var result = await _riskScoringService!.ComputeRiskAsync(
            _testClientId, "test-user", "test-correlation");

        // Assert
        result.IsSuccess.Should().BeTrue();
        var profile = result.Value!;
        profile.RiskRating.Should().Be("Low");
        profile.RiskScore.Should().BeLessThanOrEqualTo(25);
        profile.IsCurrent.Should().BeTrue();
        profile.ComputedBy.Should().Be("test-user");
        profile.RiskRulesVersion.Should().NotBeNullOrEmpty();
        profile.InputFactorsJson.Should().NotBeNullOrEmpty();
        profile.RuleExecutionLog.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ComputeRisk_SanctionsHit_ReturnsHighRating()
    {
        // Arrange - Client with sanctions hit
        await CreateTestClient(kycComplete: true, hasSanctionsHit: true);

        // Act
        var result = await _riskScoringService!.ComputeRiskAsync(_testClientId, "test-user");

        // Assert
        result.IsSuccess.Should().BeTrue();
        var profile = result.Value!;
        profile.RiskRating.Should().Be("High");
        profile.RiskScore.Should().BeGreaterThan(50);
    }

    [Fact]
    public async Task ComputeRisk_PepMatch_ReturnsHighRating()
    {
        // Arrange - Client is PEP
        await CreateTestClient(kycComplete: true, isPep: true);

        // Act
        var result = await _riskScoringService!.ComputeRiskAsync(_testClientId, "test-user");

        // Assert
        result.IsSuccess.Should().BeTrue();
        var profile = result.Value!;
        profile.RiskRating.Should().Be("High");
        profile.RiskScore.Should().BeGreaterThan(50);
    }

    [Fact]
    public async Task ComputeRisk_IncompleteKyc_IncludesPoints()
    {
        // Arrange - Incomplete KYC
        await CreateTestClient(kycComplete: false);

        // Act
        var result = await _riskScoringService!.ComputeRiskAsync(_testClientId, "test-user");

        // Assert
        result.IsSuccess.Should().BeTrue();
        var profile = result.Value!;
        // Should have at least 20 points from incomplete KYC rule
        profile.RiskScore.Should().BeGreaterThanOrEqualTo(20);
    }

    #endregion

    #region Profile Management Tests

    [Fact]
    public async Task ComputeRisk_CreatesNewProfile_SupersedesOld()
    {
        // Arrange
        await CreateTestClient();

        // Act - First computation
        var firstResult = await _riskScoringService!.ComputeRiskAsync(_testClientId, "test-user-1");
        var firstProfileId = firstResult.Value!.Id;

        // Act - Second computation
        var secondResult = await _riskScoringService.ComputeRiskAsync(_testClientId, "test-user-2");

        // Assert
        var allProfiles = await _context!.RiskProfiles
            .Where(r => r.ClientId == _testClientId)
            .OrderByDescending(r => r.ComputedAt)
            .ToListAsync();

        allProfiles.Should().HaveCount(2);

        var currentProfile = allProfiles.First();
        var supersededProfile = allProfiles.Last();

        currentProfile.IsCurrent.Should().BeTrue();
        currentProfile.ComputedBy.Should().Be("test-user-2");

        supersededProfile.Id.Should().Be(firstProfileId);
        supersededProfile.IsCurrent.Should().BeFalse();
        supersededProfile.SupersededAt.Should().NotBeNull();
        supersededProfile.SupersededReason.Should().Be("NewAssessment");
    }

    [Fact]
    public async Task GetCurrentRiskProfile_ReturnsLatestProfile()
    {
        // Arrange
        await CreateTestClient();
        await _riskScoringService!.ComputeRiskAsync(_testClientId, "test-user-1");
        var secondResult = await _riskScoringService.ComputeRiskAsync(_testClientId, "test-user-2");

        // Act
        var currentProfile = await _riskScoringService.GetCurrentRiskProfileAsync(_testClientId);

        // Assert
        currentProfile.Should().NotBeNull();
        currentProfile!.Id.Should().Be(secondResult.Value!.Id);
        currentProfile.IsCurrent.Should().BeTrue();
    }

    [Fact]
    public async Task GetRiskHistory_ReturnsAllProfiles()
    {
        // Arrange
        await CreateTestClient();
        await _riskScoringService!.ComputeRiskAsync(_testClientId, "test-user-1");
        await _riskScoringService.ComputeRiskAsync(_testClientId, "test-user-2");
        await _riskScoringService.ComputeRiskAsync(_testClientId, "test-user-3");

        // Act
        var history = await _riskScoringService.GetRiskHistoryAsync(_testClientId);

        // Assert
        history.Should().HaveCount(3);
        history.Should().BeInDescendingOrder(p => p.ComputedAt);
        history.Count(p => p.IsCurrent).Should().Be(1); // Only one current
    }

    #endregion

    #region Recompute Risk Tests

    [Fact]
    public async Task RecomputeRisk_WithReason_SupersedesWithCorrectReason()
    {
        // Arrange
        await CreateTestClient();
        await _riskScoringService!.ComputeRiskAsync(_testClientId, "initial-user");

        // Act
        var result = await _riskScoringService.RecomputeRiskAsync(
            _testClientId, "RulesUpdated", "admin-user");

        // Assert
        result.IsSuccess.Should().BeTrue();

        var history = await _context!.RiskProfiles
            .Where(r => r.ClientId == _testClientId)
            .OrderByDescending(r => r.ComputedAt)
            .ToListAsync();

        history.Should().HaveCount(2);

        var currentProfile = history.First();
        var oldProfile = history.Last();

        currentProfile.IsCurrent.Should().BeTrue();
        currentProfile.ComputedBy.Should().Be("admin-user");

        oldProfile.IsCurrent.Should().BeFalse();
        oldProfile.SupersededReason.Should().Be("RulesUpdated");
    }

    #endregion

    #region Vault Fallback Tests

    [Fact]
    public async Task ComputeRisk_VaultDisabled_UsesFallbackConfig()
    {
        // Arrange
        await CreateTestClient(kycComplete: true);

        // Act - Vault is disabled in test setup, should use fallback
        var result = await _riskScoringService!.ComputeRiskAsync(_testClientId, "test-user");

        // Assert
        result.IsSuccess.Should().BeTrue();
        var profile = result.Value!;
        profile.RiskRulesVersion.Should().Contain("fallback");
        profile.RiskScore.Should().BeGreaterThanOrEqualTo(0);
        profile.RiskScore.Should().BeLessThanOrEqualTo(100);
    }

    #endregion

    #region Input Factors Validation

    [Fact]
    public async Task BuildInputFactors_ContainsAllRequiredFields()
    {
        // Arrange
        await CreateTestClient();

        // Act
        var result = await _riskScoringService!.BuildInputFactorsAsync(_testClientId);

        // Assert
        var factors = result.Value!;
        factors.KycComplete.Should().NotBeNull();
        factors.AmlRiskLevel.Should().NotBeNullOrEmpty();
        factors.IsPep.Should().NotBeNull();
        factors.HasSanctionsHit.Should().NotBeNull();
        factors.Age.Should().BeGreaterThan(0);
        factors.Province.Should().NotBeNullOrEmpty();
        factors.ComputedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    #endregion

    #region Database Constraints Tests

    [Fact]
    public async Task RiskProfile_UniqueCurrentConstraint_Enforced()
    {
        // Arrange
        await CreateTestClient();
        await _riskScoringService!.ComputeRiskAsync(_testClientId, "test-user");

        // Act - Try to manually create another current profile (violates unique constraint)
        var duplicateProfile = new RiskProfile
        {
            Id = Guid.NewGuid(),
            ClientId = _testClientId,
            RiskRating = "Low",
            RiskScore = 10,
            ComputedAt = DateTime.UtcNow,
            ComputedBy = "test",
            RiskRulesVersion = "1.0.0",
            RiskRulesChecksum = "checksum",
            InputFactorsJson = "{}",
            IsCurrent = true // This should violate unique constraint
        };

        _context!.RiskProfiles.Add(duplicateProfile);

        // Assert
        var act = async () => await _context.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    #endregion
}
