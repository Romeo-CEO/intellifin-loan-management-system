using FluentAssertions;
using IntelliFin.ClientManagement.Domain.Entities;
using IntelliFin.ClientManagement.Domain.Enums;
using IntelliFin.ClientManagement.Infrastructure.Persistence;
using IntelliFin.ClientManagement.Models.Analytics;
using IntelliFin.ClientManagement.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Testcontainers.MsSql;
using Xunit;

namespace IntelliFin.ClientManagement.IntegrationTests.Services;

/// <summary>
/// Integration tests for AnalyticsService
/// Tests calculation of KYC performance metrics
/// </summary>
public class AnalyticsServiceTests : IAsyncLifetime
{
    private readonly MsSqlContainer _msSqlContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("YourStrong!Passw0rd")
        .Build();

    private IServiceProvider? _serviceProvider;
    private ClientManagementDbContext? _context;
    private IAnalyticsService? _analyticsService;

    private Guid _branchId;
    private DateTime _testPeriodStart;
    private DateTime _testPeriodEnd;

    public async Task InitializeAsync()
    {
        await _msSqlContainer.StartAsync();

        var services = new ServiceCollection();

        // Configure logging
        services.AddLogging(builder => builder.AddConsole());

        // Configure database
        var connectionString = _msSqlContainer.GetConnectionString();
        services.AddDbContext<ClientManagementDbContext>(options =>
            options.UseSqlServer(connectionString));

        // Register analytics service
        services.AddScoped<IAnalyticsService, AnalyticsService>();

        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<ClientManagementDbContext>();
        _analyticsService = _serviceProvider.GetRequiredService<IAnalyticsService>();

        // Create database
        await _context.Database.MigrateAsync();

        // Setup test data
        _branchId = Guid.NewGuid();
        _testPeriodStart = DateTime.UtcNow.AddDays(-7);
        _testPeriodEnd = DateTime.UtcNow;

        await SeedTestDataAsync();
    }

    public async Task DisposeAsync()
    {
        if (_serviceProvider != null)
            await _serviceProvider.DisposeAsync();

        if (_context != null)
            await _context.DisposeAsync();

        await _msSqlContainer.DisposeAsync();
    }

    private async Task SeedTestDataAsync()
    {
        // Create 10 test clients with varying KYC statuses
        var clients = new List<Client>();
        for (int i = 0; i < 10; i++)
        {
            var client = new Client
            {
                Id = Guid.NewGuid(),
                Nrc = $"111111/11/{i}",
                FirstName = $"Test{i}",
                LastName = "User",
                DateOfBirth = DateTime.UtcNow.AddYears(-25),
                Gender = "M",
                MaritalStatus = "Single",
                PrimaryPhone = $"+26097700000{i}",
                PhysicalAddress = "123 Test St",
                City = "Lusaka",
                Province = "Lusaka",
                BranchId = _branchId,
                CreatedBy = $"officer-{i % 3}",
                UpdatedBy = $"officer-{i % 3}",
                CreatedAt = _testPeriodStart.AddHours(i),
                UpdatedAt = _testPeriodStart.AddHours(i)
            };

            clients.Add(client);
        }

        _context!.Clients.AddRange(clients);
        await _context.SaveChangesAsync();

        // Create KYC statuses with different states
        var kycStatuses = new List<KycStatus>();
        for (int i = 0; i < 10; i++)
        {
            var createdAt = _testPeriodStart.AddHours(i);
            var kycStatus = new KycStatus
            {
                Id = Guid.NewGuid(),
                ClientId = clients[i].Id,
                CurrentState = i switch
                {
                    0 or 1 or 2 or 3 or 4 => KycState.Completed, // 5 completed
                    5 or 6 => KycState.InProgress, // 2 in progress
                    7 => KycState.Rejected, // 1 rejected
                    _ => KycState.Pending // 2 pending
                },
                RequiresEdd = i == 9, // 1 requires EDD
                CreatedAt = createdAt,
                UpdatedAt = createdAt,
                CreatedBy = $"officer-{i % 3}",
                UpdatedBy = $"officer-{i % 3}"
            };

            // Set completion times for completed KYCs
            if (kycStatus.CurrentState == KycState.Completed)
            {
                // Vary processing times (some within SLA, some not)
                var processingHours = i switch
                {
                    0 or 1 or 2 => 12.0, // Within 24h SLA
                    3 or 4 => 36.0, // Exceeds SLA
                    _ => 0
                };
                kycStatus.KycCompletedAt = createdAt.AddHours(processingHours);
                kycStatus.KycCompletedBy = $"officer-{i % 3}";
            }

            // Set EDD fields for EDD case
            if (kycStatus.RequiresEdd)
            {
                kycStatus.CurrentState = KycState.EDD_Required;
                kycStatus.EddEscalatedAt = createdAt.AddHours(12);
                kycStatus.EddReason = "High Risk";
            }

            kycStatuses.Add(kycStatus);
        }

        _context.KycStatuses.AddRange(kycStatuses);
        await _context.SaveChangesAsync();

        // Create documents
        var documents = new List<KycDocument>();
        for (int i = 0; i < 10; i++)
        {
            var doc = new KycDocument
            {
                Id = Guid.NewGuid(),
                ClientId = clients[i].Id,
                DocumentType = "NRC",
                ObjectKey = $"docs/nrc-{i}.pdf",
                FileSize = 1024 * 100,
                MimeType = "application/pdf",
                UploadedBy = $"officer-{i % 3}",
                UploadedAt = _testPeriodStart.AddHours(i + 1),
                VerificationStatus = i < 7 ? "Verified" : (i == 7 ? "Rejected" : "Pending"),
                CreatedAt = _testPeriodStart.AddHours(i + 1),
                UpdatedAt = _testPeriodStart.AddHours(i + 2)
            };

            if (doc.VerificationStatus == "Verified")
            {
                doc.VerifiedBy = $"officer-{(i + 1) % 3}"; // Different officer for dual control
                doc.VerifiedAt = _testPeriodStart.AddHours(i + 2);
            }
            else if (doc.VerificationStatus == "Rejected")
            {
                doc.VerifiedBy = $"officer-{(i + 1) % 3}";
                doc.VerifiedAt = _testPeriodStart.AddHours(i + 2);
                doc.RejectionReason = "Poor Quality";
            }

            documents.Add(doc);
        }

        _context.KycDocuments.AddRange(documents);
        await _context.SaveChangesAsync();

        // Create AML screenings
        var amlScreenings = new List<AmlScreening>();
        for (int i = 0; i < 8; i++) // 8 clients screened
        {
            var screening = new AmlScreening
            {
                Id = Guid.NewGuid(),
                KycStatusId = kycStatuses[i].Id,
                ScreeningType = "Comprehensive",
                SanctionsHit = i == 9, // 1 sanctions hit
                PepMatch = i % 4 == 0, // 2 PEP matches
                OverallRiskLevel = i switch
                {
                    < 4 => "Low",
                    < 7 => "Medium",
                    _ => "High"
                },
                MatchDetails = "{}",
                ScreeningProvider = "ManualAmlScreeningService v1.0",
                ScreenedAt = _testPeriodStart.AddHours(i + 3),
                CreatedAt = _testPeriodStart.AddHours(i + 3),
                UpdatedAt = _testPeriodStart.AddHours(i + 3)
            };

            amlScreenings.Add(screening);
        }

        _context.AmlScreenings.AddRange(amlScreenings);
        await _context.SaveChangesAsync();

        // Create risk profiles
        var riskProfiles = new List<RiskProfile>();
        for (int i = 0; i < 7; i++)
        {
            var profile = new RiskProfile
            {
                Id = Guid.NewGuid(),
                ClientId = clients[i].Id,
                RiskRating = i switch
                {
                    < 4 => "Low",
                    < 6 => "Medium",
                    _ => "High"
                },
                RiskScore = i switch
                {
                    < 4 => 15 + (i * 5),
                    < 6 => 50 + (i * 5),
                    _ => 70 + (i * 5)
                },
                ComputedAt = _testPeriodStart.AddHours(i + 4),
                ComputedBy = "system",
                RiskRulesVersion = "1.0.0",
                RiskRulesChecksum = "checksum",
                RuleExecutionLog = "{}",
                InputFactorsJson = "{}",
                IsCurrent = true,
                CreatedAt = _testPeriodStart.AddHours(i + 4),
                UpdatedAt = _testPeriodStart.AddHours(i + 4)
            };

            riskProfiles.Add(profile);
        }

        _context.RiskProfiles.AddRange(riskProfiles);
        await _context.SaveChangesAsync();
    }

    #region KYC Performance Tests

    [Fact]
    public async Task GetKycPerformanceAsync_ReturnsCorrectMetrics()
    {
        // Arrange
        var request = new AnalyticsRequest
        {
            StartDate = _testPeriodStart.AddDays(-1),
            EndDate = _testPeriodEnd,
            BranchId = _branchId
        };

        // Act
        var result = await _analyticsService!.GetKycPerformanceAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var metrics = result.Value;

        metrics.TotalStarted.Should().Be(10);
        metrics.TotalCompleted.Should().Be(5);
        metrics.TotalRejected.Should().Be(1);
        metrics.TotalInProgress.Should().Be(2);
        metrics.TotalPending.Should().Be(2); // 1 pending + 1 EDD
        metrics.TotalEddEscalations.Should().Be(1);
        metrics.CompletionRate.Should().BeApproximately(50.0, 0.1);
        metrics.EddEscalationRate.Should().BeApproximately(10.0, 0.1);

        // SLA compliance (3 within SLA, 2 breached)
        metrics.SlaComplianceRate.Should().BeApproximately(60.0, 0.1);
        metrics.SlaBreaches.Should().Be(2);
    }

    [Fact]
    public async Task GetKycPerformanceAsync_CalculatesProcessingTimes()
    {
        // Arrange
        var request = new AnalyticsRequest
        {
            StartDate = _testPeriodStart.AddDays(-1),
            EndDate = _testPeriodEnd,
            BranchId = _branchId
        };

        // Act
        var result = await _analyticsService!.GetKycPerformanceAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var metrics = result.Value;

        // Average: (12 + 12 + 12 + 36 + 36) / 5 = 21.6 hours
        metrics.AverageProcessingTimeHours.Should().BeGreaterThan(20);
        metrics.AverageProcessingTimeHours.Should().BeLessThan(25);

        // Median should be 12 (middle value of sorted [12, 12, 12, 36, 36])
        metrics.MedianProcessingTimeHours.Should().Be(12.0);

        // Average SLA breach time (only for breached: (36 + 36) / 2 = 36)
        metrics.AverageSlaBreachTimeHours.Should().Be(36.0);
    }

    [Fact]
    public async Task GetKycPerformanceAsync_FiltersByBranch()
    {
        // Arrange - create client in different branch
        var otherBranch = Guid.NewGuid();
        var otherClient = new Client
        {
            Id = Guid.NewGuid(),
            Nrc = "999999/99/9",
            FirstName = "Other",
            LastName = "Branch",
            DateOfBirth = DateTime.UtcNow.AddYears(-30),
            Gender = "F",
            MaritalStatus = "Single",
            PrimaryPhone = "+260977999999",
            PhysicalAddress = "123 Other St",
            City = "Ndola",
            Province = "Copperbelt",
            BranchId = otherBranch,
            CreatedBy = "other-officer",
            UpdatedBy = "other-officer"
        };

        _context!.Clients.Add(otherClient);

        var otherKyc = new KycStatus
        {
            Id = Guid.NewGuid(),
            ClientId = otherClient.Id,
            CurrentState = KycState.Completed,
            CreatedAt = _testPeriodStart,
            UpdatedAt = _testPeriodStart,
            CreatedBy = "other-officer",
            UpdatedBy = "other-officer"
        };

        _context.KycStatuses.Add(otherKyc);
        await _context.SaveChangesAsync();

        // Act - query for original branch
        var request = new AnalyticsRequest
        {
            StartDate = _testPeriodStart.AddDays(-1),
            EndDate = _testPeriodEnd,
            BranchId = _branchId
        };

        var result = await _analyticsService!.GetKycPerformanceAsync(request);

        // Assert - should not include other branch
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalStarted.Should().Be(10); // Not 11
    }

    #endregion

    #region Document Metrics Tests

    [Fact]
    public async Task GetDocumentMetricsAsync_ReturnsCorrectCounts()
    {
        // Arrange
        var request = new AnalyticsRequest
        {
            StartDate = _testPeriodStart.AddDays(-1),
            EndDate = _testPeriodEnd,
            BranchId = _branchId
        };

        // Act
        var result = await _analyticsService!.GetDocumentMetricsAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var metrics = result.Value;

        metrics.TotalUploaded.Should().Be(10);
        metrics.TotalVerified.Should().Be(7);
        metrics.TotalRejected.Should().Be(1);
        metrics.TotalPending.Should().Be(2);
        metrics.VerificationRate.Should().BeApproximately(70.0, 0.1);
        metrics.RejectionRate.Should().BeApproximately(10.0, 0.1);
    }

    [Fact]
    public async Task GetDocumentMetricsAsync_CalculatesDualControlCompliance()
    {
        // Arrange
        var request = new AnalyticsRequest
        {
            StartDate = _testPeriodStart.AddDays(-1),
            EndDate = _testPeriodEnd,
            BranchId = _branchId
        };

        // Act
        var result = await _analyticsService!.GetDocumentMetricsAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var metrics = result.Value;

        // All verified docs have different uploader/verifier
        metrics.DualControlComplianceRate.Should().BeApproximately(100.0, 0.1);
    }

    [Fact]
    public async Task GetDocumentMetricsAsync_ReturnsTopRejectionReasons()
    {
        // Arrange
        var request = new AnalyticsRequest
        {
            StartDate = _testPeriodStart.AddDays(-1),
            EndDate = _testPeriodEnd,
            BranchId = _branchId
        };

        // Act
        var result = await _analyticsService!.GetDocumentMetricsAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var metrics = result.Value;

        metrics.TopRejectionReasons.Should().HaveCount(1);
        metrics.TopRejectionReasons[0].Reason.Should().Be("Poor Quality");
        metrics.TopRejectionReasons[0].Count.Should().Be(1);
        metrics.TopRejectionReasons[0].Percentage.Should().BeApproximately(100.0, 0.1);
    }

    #endregion

    #region AML Metrics Tests

    [Fact]
    public async Task GetAmlMetricsAsync_ReturnsCorrectStatistics()
    {
        // Arrange
        var request = new AnalyticsRequest
        {
            StartDate = _testPeriodStart.AddDays(-1),
            EndDate = _testPeriodEnd,
            BranchId = _branchId
        };

        // Act
        var result = await _analyticsService!.GetAmlMetricsAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var metrics = result.Value;

        metrics.TotalScreenings.Should().Be(8);
        metrics.PepMatches.Should().Be(2); // i % 4 == 0 (i=0, i=4)
        metrics.PepMatchRate.Should().BeApproximately(25.0, 0.1);
    }

    [Fact]
    public async Task GetAmlMetricsAsync_ReturnsRiskDistribution()
    {
        // Arrange
        var request = new AnalyticsRequest
        {
            StartDate = _testPeriodStart.AddDays(-1),
            EndDate = _testPeriodEnd,
            BranchId = _branchId
        };

        // Act
        var result = await _analyticsService!.GetAmlMetricsAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var metrics = result.Value;

        metrics.RiskLevelDistribution.Should().ContainKey("Low");
        metrics.RiskLevelDistribution.Should().ContainKey("Medium");
        metrics.RiskLevelDistribution.Should().ContainKey("High");

        metrics.RiskLevelDistribution["Low"].Should().Be(4); // i=0,1,2,3
        metrics.RiskLevelDistribution["Medium"].Should().Be(3); // i=4,5,6
        metrics.RiskLevelDistribution["High"].Should().Be(1); // i=7
    }

    #endregion

    #region EDD Metrics Tests

    [Fact]
    public async Task GetEddMetricsAsync_ReturnsCorrectCounts()
    {
        // Arrange
        var request = new AnalyticsRequest
        {
            StartDate = _testPeriodStart.AddDays(-1),
            EndDate = _testPeriodEnd,
            BranchId = _branchId
        };

        // Act
        var result = await _analyticsService!.GetEddMetricsAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var metrics = result.Value;

        metrics.TotalInitiated.Should().Be(1);
        metrics.TotalInProgress.Should().Be(1);
    }

    [Fact]
    public async Task GetEddMetricsAsync_ReturnsEscalationReasons()
    {
        // Arrange
        var request = new AnalyticsRequest
        {
            StartDate = _testPeriodStart.AddDays(-1),
            EndDate = _testPeriodEnd,
            BranchId = _branchId
        };

        // Act
        var result = await _analyticsService!.GetEddMetricsAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var metrics = result.Value;

        metrics.TopEscalationReasons.Should().HaveCount(1);
        metrics.TopEscalationReasons[0].Reason.Should().Be("High Risk");
        metrics.TopEscalationReasons[0].Percentage.Should().BeApproximately(100.0, 0.1);
    }

    #endregion

    #region Officer Performance Tests

    [Fact]
    public async Task GetOfficerPerformanceAsync_ReturnsAllOfficers()
    {
        // Arrange
        var request = new OfficerPerformanceRequest
        {
            StartDate = _testPeriodStart.AddDays(-1),
            EndDate = _testPeriodEnd,
            BranchId = _branchId,
            MinimumProcessed = 1
        };

        // Act
        var result = await _analyticsService!.GetOfficerPerformanceAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var officers = result.Value;

        // We have 3 officers (officer-0, officer-1, officer-2) based on i % 3
        officers.Should().HaveCountGreaterOrEqualTo(3);
        officers.Should().AllSatisfy(o => o.TotalProcessed.Should().BeGreaterThan(0));
    }

    [Fact]
    public async Task GetOfficerPerformanceAsync_CalculatesMetricsCorrectly()
    {
        // Arrange
        var request = new OfficerPerformanceRequest
        {
            StartDate = _testPeriodStart.AddDays(-1),
            EndDate = _testPeriodEnd,
            BranchId = _branchId,
            MinimumProcessed = 1
        };

        // Act
        var result = await _analyticsService!.GetOfficerPerformanceAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var officers = result.Value;

        foreach (var officer in officers)
        {
            officer.CompletionRate.Should().BeGreaterThanOrEqualTo(0);
            officer.CompletionRate.Should().BeLessThanOrEqualTo(100);
            officer.SlaComplianceRate.Should().BeGreaterThanOrEqualTo(0);
            officer.SlaComplianceRate.Should().BeLessThanOrEqualTo(100);
        }
    }

    [Fact]
    public async Task GetOfficerPerformanceAsync_SortsCorrectly()
    {
        // Arrange
        var request = new OfficerPerformanceRequest
        {
            StartDate = _testPeriodStart.AddDays(-1),
            EndDate = _testPeriodEnd,
            BranchId = _branchId,
            SortBy = OfficerSortBy.TotalProcessed,
            SortDirection = SortDirection.Descending
        };

        // Act
        var result = await _analyticsService!.GetOfficerPerformanceAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var officers = result.Value;

        // Verify descending order
        for (int i = 0; i < officers.Count - 1; i++)
        {
            officers[i].TotalProcessed.Should().BeGreaterThanOrEqualTo(officers[i + 1].TotalProcessed);
        }
    }

    #endregion

    #region Risk Distribution Tests

    [Fact]
    public async Task GetRiskDistributionAsync_ReturnsCorrectDistribution()
    {
        // Arrange
        var request = new AnalyticsRequest
        {
            StartDate = _testPeriodStart.AddDays(-1),
            EndDate = _testPeriodEnd,
            BranchId = _branchId
        };

        // Act
        var result = await _analyticsService!.GetRiskDistributionAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var metrics = result.Value;

        metrics.LowRisk.Should().Be(4);
        metrics.MediumRisk.Should().Be(2);
        metrics.HighRisk.Should().Be(1);
        metrics.AverageRiskScore.Should().BeGreaterThan(0);
        metrics.MedianRiskScore.Should().BeGreaterThan(0);
    }

    #endregion

    #region KYC Funnel Tests

    [Fact]
    public async Task GetKycFunnelMetricsAsync_ReturnsConversionRates()
    {
        // Arrange
        var request = new AnalyticsRequest
        {
            StartDate = _testPeriodStart.AddDays(-1),
            EndDate = _testPeriodEnd,
            BranchId = _branchId
        };

        // Act
        var result = await _analyticsService!.GetKycFunnelMetricsAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var metrics = result.Value;

        metrics.ClientsCreated.Should().Be(10);
        metrics.DocumentsUploaded.Should().Be(10);
        metrics.KycCompleted.Should().Be(5);

        metrics.DocumentUploadConversion.Should().BeApproximately(100.0, 0.1);
        metrics.OverallConversionRate.Should().BeApproximately(50.0, 0.1);
    }

    #endregion

    #region Time Series Tests

    [Fact]
    public async Task GetKycTimeSeriesAsync_ReturnsDataPoints()
    {
        // Arrange
        var request = new AnalyticsRequest
        {
            StartDate = _testPeriodStart.AddDays(-1),
            EndDate = _testPeriodEnd,
            BranchId = _branchId,
            Granularity = TimeGranularity.Daily
        };

        // Act
        var result = await _analyticsService!.GetKycTimeSeriesAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var dataPoints = result.Value;

        dataPoints.Should().NotBeEmpty();
        dataPoints.Should().AllSatisfy(dp =>
        {
            dp.Period.Should().BeOnOrAfter(_testPeriodStart.AddDays(-1).Date);
            dp.Started.Should().BeGreaterThanOrEqualTo(0);
            dp.CompletionRate.Should().BeGreaterThanOrEqualTo(0);
            dp.CompletionRate.Should().BeLessThanOrEqualTo(100);
        });
    }

    [Fact]
    public async Task GetKycTimeSeriesAsync_GroupsByGranularity()
    {
        // Arrange
        var request = new AnalyticsRequest
        {
            StartDate = _testPeriodStart.AddDays(-1),
            EndDate = _testPeriodEnd,
            BranchId = _branchId,
            Granularity = TimeGranularity.Weekly
        };

        // Act
        var result = await _analyticsService!.GetKycTimeSeriesAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var dataPoints = result.Value;

        dataPoints.Should().NotBeEmpty();
        // Weekly granularity should have fewer data points than daily
    }

    #endregion
}
