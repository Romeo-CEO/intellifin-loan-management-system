using FluentAssertions;
using IntelliFin.ClientManagement.Domain.Entities;
using IntelliFin.ClientManagement.Domain.Enums;
using IntelliFin.ClientManagement.Infrastructure.Persistence;
using IntelliFin.ClientManagement.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Testcontainers.MsSql;
using Xunit;

namespace IntelliFin.ClientManagement.IntegrationTests.Services;

/// <summary>
/// Integration tests for EDD report generation
/// Tests report content, structure, and accuracy
/// </summary>
public class EddReportGenerationTests : IAsyncLifetime
{
    private readonly MsSqlContainer _msSqlContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("YourStrong!Passw0rd")
        .Build();

    private ClientManagementDbContext? _context;
    private EddReportGenerator? _reportGenerator;
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
        _reportGenerator = new EddReportGenerator(
            _context,
            loggerFactory.CreateLogger<EddReportGenerator>());
    }

    public async Task DisposeAsync()
    {
        if (_context != null)
            await _context.DisposeAsync();
        await _msSqlContainer.DisposeAsync();
    }

    private async Task CreateTestScenario(
        string firstName = "John",
        string lastName = "Banda",
        bool hasSanctionsHit = false,
        bool hasPepMatch = false,
        bool documentComplete = true)
    {
        // Create client
        var client = new Client
        {
            Id = Guid.NewGuid(),
            Nrc = "123456/11/1",
            FirstName = firstName,
            LastName = lastName,
            DateOfBirth = new DateTime(1980, 5, 15),
            Gender = "M",
            MaritalStatus = "Married",
            PrimaryPhone = "+260977123456",
            EmailAddress = "john.banda@example.com",
            PhysicalAddress = "123 Independence Ave",
            City = "Lusaka",
            Province = "Lusaka",
            Employer = "ABC Corporation",
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
            CurrentState = KycState.EDD_Required,
            KycStartedAt = DateTime.UtcNow.AddDays(-7),
            HasNrc = documentComplete,
            HasProofOfAddress = documentComplete,
            HasPayslip = documentComplete,
            HasEmploymentLetter = false,
            AmlScreeningComplete = true,
            AmlScreenedAt = DateTime.UtcNow.AddDays(-1),
            AmlScreenedBy = "system-workflow",
            RequiresEdd = true,
            EddReason = hasSanctionsHit ? "Sanctions" : hasPepMatch ? "PEP" : "HighRisk",
            EddEscalatedAt = DateTime.UtcNow.AddHours(-2),
            CreatedAt = DateTime.UtcNow.AddDays(-7),
            UpdatedAt = DateTime.UtcNow
        };

        _context.KycStatuses.Add(kycStatus);
        _testKycStatusId = kycStatus.Id;

        // Create documents if complete
        if (documentComplete)
        {
            var documents = new[]
            {
                new ClientDocument
                {
                    Id = Guid.NewGuid(),
                    ClientId = client.Id,
                    DocumentType = "NRC",
                    Category = "KYC",
                    ObjectKey = $"kyc/{client.Id}/nrc.pdf",
                    BucketName = "kyc-documents",
                    FileName = "nrc.pdf",
                    ContentType = "application/pdf",
                    FileSizeBytes = 1024,
                    FileHashSha256 = "hash1",
                    UploadStatus = UploadStatus.Verified,
                    UploadedBy = "user1",
                    UploadedAt = DateTime.UtcNow.AddDays(-5),
                    VerifiedBy = "user2",
                    VerifiedAt = DateTime.UtcNow.AddDays(-4),
                    RetentionUntil = DateTime.UtcNow.AddYears(7),
                    CreatedAt = DateTime.UtcNow.AddDays(-5)
                },
                new ClientDocument
                {
                    Id = Guid.NewGuid(),
                    ClientId = client.Id,
                    DocumentType = "ProofOfAddress",
                    Category = "KYC",
                    ObjectKey = $"kyc/{client.Id}/address.pdf",
                    BucketName = "kyc-documents",
                    FileName = "address.pdf",
                    ContentType = "application/pdf",
                    FileSizeBytes = 2048,
                    FileHashSha256 = "hash2",
                    UploadStatus = UploadStatus.Verified,
                    UploadedBy = "user1",
                    UploadedAt = DateTime.UtcNow.AddDays(-5),
                    VerifiedBy = "user2",
                    VerifiedAt = DateTime.UtcNow.AddDays(-4),
                    RetentionUntil = DateTime.UtcNow.AddYears(7),
                    CreatedAt = DateTime.UtcNow.AddDays(-5)
                },
                new ClientDocument
                {
                    Id = Guid.NewGuid(),
                    ClientId = client.Id,
                    DocumentType = "Payslip",
                    Category = "KYC",
                    ObjectKey = $"kyc/{client.Id}/payslip.pdf",
                    BucketName = "kyc-documents",
                    FileName = "payslip.pdf",
                    ContentType = "application/pdf",
                    FileSizeBytes = 3072,
                    FileHashSha256 = "hash3",
                    UploadStatus = UploadStatus.Verified,
                    UploadedBy = "user1",
                    UploadedAt = DateTime.UtcNow.AddDays(-5),
                    VerifiedBy = "user2",
                    VerifiedAt = DateTime.UtcNow.AddDays(-4),
                    RetentionUntil = DateTime.UtcNow.AddYears(7),
                    CreatedAt = DateTime.UtcNow.AddDays(-5)
                }
            };

            _context.ClientDocuments.AddRange(documents);
        }

        // Create AML screenings
        if (hasSanctionsHit)
        {
            var sanctionsScreening = new AmlScreening
            {
                Id = Guid.NewGuid(),
                KycStatusId = kycStatus.Id,
                ScreeningType = "Sanctions",
                ScreeningProvider = "Manual_v2_Fuzzy",
                ScreenedAt = DateTime.UtcNow.AddDays(-1),
                ScreenedBy = "system-workflow",
                IsMatch = true,
                MatchDetails = "{\"matchedName\":\"Sanctioned Person\",\"matchConfidence\":100}",
                RiskLevel = "High",
                Notes = "Sanctions match detected",
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            };
            _context.AmlScreenings.Add(sanctionsScreening);
        }
        else
        {
            // No sanctions match
            var sanctionsScreening = new AmlScreening
            {
                Id = Guid.NewGuid(),
                KycStatusId = kycStatus.Id,
                ScreeningType = "Sanctions",
                ScreeningProvider = "Manual_v2_Fuzzy",
                ScreenedAt = DateTime.UtcNow.AddDays(-1),
                ScreenedBy = "system-workflow",
                IsMatch = false,
                RiskLevel = "Clear",
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            };
            _context.AmlScreenings.Add(sanctionsScreening);
        }

        if (hasPepMatch)
        {
            var pepScreening = new AmlScreening
            {
                Id = Guid.NewGuid(),
                KycStatusId = kycStatus.Id,
                ScreeningType = "PEP",
                ScreeningProvider = "Manual_v2_Fuzzy_Zambia",
                ScreenedAt = DateTime.UtcNow.AddDays(-1),
                ScreenedBy = "system-workflow",
                IsMatch = true,
                MatchDetails = "{\"matchedName\":\"Political Figure\",\"position\":\"Minister\"}",
                RiskLevel = "High",
                Notes = "PEP match: Political Figure",
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            };
            _context.AmlScreenings.Add(pepScreening);
        }
        else
        {
            // No PEP match
            var pepScreening = new AmlScreening
            {
                Id = Guid.NewGuid(),
                KycStatusId = kycStatus.Id,
                ScreeningType = "PEP",
                ScreeningProvider = "Manual_v2_Fuzzy_Zambia",
                ScreenedAt = DateTime.UtcNow.AddDays(-1),
                ScreenedBy = "system-workflow",
                IsMatch = false,
                RiskLevel = "Clear",
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            };
            _context.AmlScreenings.Add(pepScreening);
        }

        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task GenerateReport_ValidData_ReturnsSuccess()
    {
        // Arrange
        await CreateTestScenario();

        // Act
        var result = await _reportGenerator!.GenerateReportAsync(
            _testClientId, _testKycStatusId, "test-correlation");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.ClientId.Should().Be(_testClientId);
        result.Value.KycStatusId.Should().Be(_testKycStatusId);
        result.Value.ReportContent.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GenerateReport_ContainsExecutiveSummary()
    {
        // Arrange
        await CreateTestScenario();

        // Act
        var result = await _reportGenerator!.GenerateReportAsync(
            _testClientId, _testKycStatusId, "test-correlation");

        // Assert
        var report = result.Value!.ReportContent;
        report.Should().Contain("ENHANCED DUE DILIGENCE (EDD) REPORT");
        report.Should().Contain("EXECUTIVE SUMMARY");
        report.Should().Contain("John Banda");
        report.Should().Contain("123456/11/1");
        report.Should().Contain("OVERALL RISK LEVEL");
    }

    [Fact]
    public async Task GenerateReport_ContainsClientProfile()
    {
        // Arrange
        await CreateTestScenario();

        // Act
        var result = await _reportGenerator!.GenerateReportAsync(
            _testClientId, _testKycStatusId, "test-correlation");

        // Assert
        var report = result.Value!.ReportContent;
        report.Should().Contain("CLIENT PROFILE ANALYSIS");
        report.Should().Contain("PERSONAL INFORMATION");
        report.Should().Contain("john.banda@example.com");
        report.Should().Contain("123 Independence Ave");
        report.Should().Contain("Lusaka");
        report.Should().Contain("ABC Corporation");
    }

    [Fact]
    public async Task GenerateReport_ContainsDocumentAnalysis()
    {
        // Arrange
        await CreateTestScenario(documentComplete: true);

        // Act
        var result = await _reportGenerator!.GenerateReportAsync(
            _testClientId, _testKycStatusId, "test-correlation");

        // Assert
        var report = result.Value!.ReportContent;
        report.Should().Contain("DOCUMENT VERIFICATION RESULTS");
        report.Should().Contain("✓ Verified"); // Documents verified
        report.Should().Contain("NRC Document");
        report.Should().Contain("Proof of Address");
        report.Should().Contain("Payslip");
    }

    [Fact]
    public async Task GenerateReport_IncompleteDocuments_ShowsMissing()
    {
        // Arrange
        await CreateTestScenario(documentComplete: false);

        // Act
        var result = await _reportGenerator!.GenerateReportAsync(
            _testClientId, _testKycStatusId, "test-correlation");

        // Assert
        var report = result.Value!.ReportContent;
        report.Should().Contain("✗ Missing"); // Some documents missing
        report.Should().Contain("INCOMPLETE");
    }

    [Fact]
    public async Task GenerateReport_ContainsAmlScreeningResults()
    {
        // Arrange
        await CreateTestScenario();

        // Act
        var result = await _reportGenerator!.GenerateReportAsync(
            _testClientId, _testKycStatusId, "test-correlation");

        // Assert
        var report = result.Value!.ReportContent;
        report.Should().Contain("AML SCREENING DETAILED RESULTS");
        report.Should().Contain("SCREENING TYPE: Sanctions");
        report.Should().Contain("SCREENING TYPE: PEP");
        report.Should().Contain("Manual_v2_Fuzzy");
    }

    [Fact]
    public async Task GenerateReport_SanctionsHit_ShowsWarning()
    {
        // Arrange
        await CreateTestScenario(hasSanctionsHit: true);

        // Act
        var result = await _reportGenerator!.GenerateReportAsync(
            _testClientId, _testKycStatusId, "test-correlation");

        // Assert
        var report = result.Value!.ReportContent;
        report.Should().Contain("⚠ SANCTIONS LIST MATCH DETECTED");
        report.Should().Contain("⚠ MATCH FOUND");
        result.Value.OverallRiskLevel.Should().Be("High");
    }

    [Fact]
    public async Task GenerateReport_PepMatch_ShowsWarning()
    {
        // Arrange
        await CreateTestScenario(hasPepMatch: true);

        // Act
        var result = await _reportGenerator!.GenerateReportAsync(
            _testClientId, _testKycStatusId, "test-correlation");

        // Assert
        var report = result.Value!.ReportContent;
        report.Should().Contain("⚠ POLITICALLY EXPOSED PERSON (PEP) IDENTIFIED");
        result.Value.OverallRiskLevel.Should().Be("High");
    }

    [Fact]
    public async Task GenerateReport_ContainsRiskAssessment()
    {
        // Arrange
        await CreateTestScenario();

        // Act
        var result = await _reportGenerator!.GenerateReportAsync(
            _testClientId, _testKycStatusId, "test-correlation");

        // Assert
        var report = result.Value!.ReportContent;
        report.Should().Contain("RISK ASSESSMENT BREAKDOWN");
        report.Should().Contain("Overall Risk Score");
        report.Should().Contain("Risk Rating");
        report.Should().Contain("CONTRIBUTING RISK FACTORS");
        report.Should().Contain("MITIGATING FACTORS");
    }

    [Fact]
    public async Task GenerateReport_ContainsComplianceRecommendation()
    {
        // Arrange
        await CreateTestScenario();

        // Act
        var result = await _reportGenerator!.GenerateReportAsync(
            _testClientId, _testKycStatusId, "test-correlation");

        // Assert
        var report = result.Value!.ReportContent;
        report.Should().Contain("COMPLIANCE RECOMMENDATION");
        report.Should().Contain("Enhanced Due Diligence review");
        report.Should().Contain("REQUIRED ACTIONS");
        report.Should().Contain("Compliance Officer review");
        report.Should().Contain("CEO final approval");
        report.Should().Contain("MONITORING REQUIREMENTS");
    }

    [Fact]
    public async Task GenerateReport_InvalidClientId_ReturnsFailure()
    {
        // Arrange
        var invalidClientId = Guid.NewGuid();

        // Act
        var result = await _reportGenerator!.GenerateReportAsync(
            invalidClientId, Guid.NewGuid(), "test-correlation");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task GenerateReport_InvalidKycStatusId_ReturnsFailure()
    {
        // Arrange
        await CreateTestScenario();
        var invalidKycStatusId = Guid.NewGuid();

        // Act
        var result = await _reportGenerator!.GenerateReportAsync(
            _testClientId, invalidKycStatusId, "test-correlation");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task GenerateReport_PopulatesAllMetadata()
    {
        // Arrange
        await CreateTestScenario(firstName: "Vladimir", lastName: "Putin", hasSanctionsHit: true);

        // Act
        var result = await _reportGenerator!.GenerateReportAsync(
            _testClientId, _testKycStatusId, "test-correlation-123");

        // Assert
        var data = result.Value!;
        data.ClientId.Should().Be(_testClientId);
        data.KycStatusId.Should().Be(_testKycStatusId);
        data.ClientName.Should().Be("Vladimir Putin");
        data.OverallRiskLevel.Should().Be("High");
        data.EddReason.Should().Be("Sanctions");
        data.CorrelationId.Should().Be("test-correlation-123");
        data.GeneratedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GenerateReport_CleanClient_LowRisk()
    {
        // Arrange
        await CreateTestScenario(hasSanctionsHit: false, hasPepMatch: false, documentComplete: true);

        // Act
        var result = await _reportGenerator!.GenerateReportAsync(
            _testClientId, _testKycStatusId, "test-correlation");

        // Assert
        result.Value!.OverallRiskLevel.Should().Be("Low");
        result.Value.ReportContent.Should().Contain("✓ All required documents verified");
        result.Value.ReportContent.Should().Contain("✓ AML screening completed");
    }
}
