using FluentAssertions;
using IntelliFin.ClientManagement.Domain.Entities;
using IntelliFin.ClientManagement.Domain.Enums;
using IntelliFin.ClientManagement.Infrastructure.Persistence;
using IntelliFin.ClientManagement.Services;
using IntelliFin.ClientManagement.Workflows.CamundaWorkers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Testcontainers.MsSql;
using Xunit;
using Zeebe.Client.Api.Responses;
using Zeebe.Client.Api.Worker;

namespace IntelliFin.ClientManagement.IntegrationTests.Workflows;

/// <summary>
/// Integration tests for KYC workflow workers
/// Tests document checking, AML screening, and risk assessment
/// </summary>
public class KycWorkflowWorkersTests : IAsyncLifetime
{
    private readonly MsSqlContainer _msSqlContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("YourStrong!Passw0rd")
        .Build();

    private ClientManagementDbContext? _context;
    private Mock<IAuditService>? _mockAuditService;
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

        // Create test client
        var testClient = new Client
        {
            Id = Guid.NewGuid(),
            Nrc = "111111/11/1",
            FirstName = "John",
            LastName = "Banda",
            DateOfBirth = new DateTime(1990, 5, 15),
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

        _context.Clients.Add(testClient);
        await _context.SaveChangesAsync();
        _testClientId = testClient.Id;

        // Create KYC status
        var kycStatus = new KycStatus
        {
            Id = Guid.NewGuid(),
            ClientId = _testClientId,
            CurrentState = KycState.InProgress,
            KycStartedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.KycStatuses.Add(kycStatus);
        await _context.SaveChangesAsync();
        _testKycStatusId = kycStatus.Id;

        _mockAuditService = new Mock<IAuditService>();
    }

    public async Task DisposeAsync()
    {
        if (_context != null)
            await _context.DisposeAsync();
        await _msSqlContainer.DisposeAsync();
    }

    #region KycDocumentCheckWorker Tests

    [Fact]
    public async Task KycDocumentCheckWorker_AllDocumentsVerified_ShouldReturnComplete()
    {
        // Arrange
        await CreateVerifiedDocument(_testClientId, "NRC");
        await CreateVerifiedDocument(_testClientId, "ProofOfAddress");
        await CreateVerifiedDocument(_testClientId, "Payslip");

        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var kycService = new KycWorkflowService(_context!, _mockAuditService!.Object,
            loggerFactory.CreateLogger<KycWorkflowService>());

        var worker = new KycDocumentCheckWorker(
            loggerFactory.CreateLogger<KycDocumentCheckWorker>(),
            _context!,
            kycService);

        var mockJob = CreateMockJob(new Dictionary<string, object>
        {
            ["clientId"] = _testClientId.ToString()
        });

        var mockJobClient = new Mock<IJobClient>();
        var completedVariables = new Dictionary<string, object>();

        mockJobClient.Setup(x => x.NewCompleteJobCommand(It.IsAny<long>()))
            .Returns(new MockCompleteJobCommand(completedVariables));

        // Act
        await worker.HandleJobAsync(mockJobClient.Object, mockJob.Object);

        // Assert
        completedVariables["documentComplete"].Should().Be(true);
        completedVariables["hasNrc"].Should().Be(true);
        completedVariables["hasProofOfAddress"].Should().Be(true);
        completedVariables["hasPayslip"].Should().Be(true);

        // Verify KYC status updated
        var kycStatus = await _context!.KycStatuses.FindAsync(_testKycStatusId);
        kycStatus!.HasNrc.Should().BeTrue();
        kycStatus.HasProofOfAddress.Should().BeTrue();
        kycStatus.HasPayslip.Should().BeTrue();
        kycStatus.IsDocumentComplete.Should().BeTrue();
    }

    [Fact]
    public async Task KycDocumentCheckWorker_MissingNrc_ShouldReturnIncomplete()
    {
        // Arrange - Only proof of address and payslip, no NRC
        await CreateVerifiedDocument(_testClientId, "ProofOfAddress");
        await CreateVerifiedDocument(_testClientId, "Payslip");

        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var kycService = new KycWorkflowService(_context!, _mockAuditService!.Object,
            loggerFactory.CreateLogger<KycWorkflowService>());

        var worker = new KycDocumentCheckWorker(
            loggerFactory.CreateLogger<KycDocumentCheckWorker>(),
            _context!,
            kycService);

        var mockJob = CreateMockJob(new Dictionary<string, object>
        {
            ["clientId"] = _testClientId.ToString()
        });

        var mockJobClient = new Mock<IJobClient>();
        var completedVariables = new Dictionary<string, object>();

        mockJobClient.Setup(x => x.NewCompleteJobCommand(It.IsAny<long>()))
            .Returns(new MockCompleteJobCommand(completedVariables));

        // Act
        await worker.HandleJobAsync(mockJobClient.Object, mockJob.Object);

        // Assert
        completedVariables["documentComplete"].Should().Be(false);
        completedVariables["hasNrc"].Should().Be(false);
        completedVariables["hasProofOfAddress"].Should().Be(true);
    }

    #endregion

    #region AmlScreeningWorker Tests

    [Fact]
    public async Task AmlScreeningWorker_NoMatches_ShouldReturnClearRisk()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var amlService = new ManualAmlScreeningService(_context!,
            loggerFactory.CreateLogger<ManualAmlScreeningService>());

        var worker = new AmlScreeningWorker(
            loggerFactory.CreateLogger<AmlScreeningWorker>(),
            _context!,
            amlService);

        var mockJob = CreateMockJob(new Dictionary<string, object>
        {
            ["clientId"] = _testClientId.ToString()
        });

        var mockJobClient = new Mock<IJobClient>();
        var completedVariables = new Dictionary<string, object>();

        mockJobClient.Setup(x => x.NewCompleteJobCommand(It.IsAny<long>()))
            .Returns(new MockCompleteJobCommand(completedVariables));

        // Act
        await worker.HandleJobAsync(mockJobClient.Object, mockJob.Object);

        // Assert
        completedVariables["amlRiskLevel"].Should().Be("Clear");
        completedVariables["sanctionsHit"].Should().Be(false);
        completedVariables["pepMatch"].Should().Be(false);
        completedVariables["amlScreeningComplete"].Should().Be(true);

        // Verify screening records created
        var screenings = await _context!.AmlScreenings
            .Where(a => a.KycStatusId == _testKycStatusId)
            .ToListAsync();

        screenings.Should().HaveCount(2); // Sanctions + PEP
        screenings.Should().AllSatisfy(s => s.IsMatch.Should().BeFalse());
    }

    [Fact]
    public async Task AmlScreeningWorker_SanctionsHit_ShouldReturnHighRisk()
    {
        // Arrange - Create client with sanctioned name
        var sanctionedClient = new Client
        {
            Id = Guid.NewGuid(),
            Nrc = "222222/22/2",
            FirstName = "Sanctioned",
            LastName = "Person", // Matches hardcoded sanctions list
            DateOfBirth = new DateTime(1980, 1, 1),
            Gender = "M",
            MaritalStatus = "Single",
            PrimaryPhone = "+260977222222",
            PhysicalAddress = "456 Test St",
            City = "Lusaka",
            Province = "Lusaka",
            BranchId = Guid.NewGuid(),
            CreatedBy = "test-user",
            UpdatedBy = "test-user"
        };

        _context!.Clients.Add(sanctionedClient);

        var sanctionedKycStatus = new KycStatus
        {
            Id = Guid.NewGuid(),
            ClientId = sanctionedClient.Id,
            CurrentState = KycState.InProgress,
            KycStartedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.KycStatuses.Add(sanctionedKycStatus);
        await _context.SaveChangesAsync();

        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var amlService = new ManualAmlScreeningService(_context,
            loggerFactory.CreateLogger<ManualAmlScreeningService>());

        var worker = new AmlScreeningWorker(
            loggerFactory.CreateLogger<AmlScreeningWorker>(),
            _context,
            amlService);

        var mockJob = CreateMockJob(new Dictionary<string, object>
        {
            ["clientId"] = sanctionedClient.Id.ToString()
        });

        var mockJobClient = new Mock<IJobClient>();
        var completedVariables = new Dictionary<string, object>();

        mockJobClient.Setup(x => x.NewCompleteJobCommand(It.IsAny<long>()))
            .Returns(new MockCompleteJobCommand(completedVariables));

        // Act
        await worker.HandleJobAsync(mockJobClient.Object, mockJob.Object);

        // Assert
        completedVariables["amlRiskLevel"].Should().Be("High");
        completedVariables["sanctionsHit"].Should().Be(true);

        // Verify KYC status flagged for EDD
        var updatedKycStatus = await _context.KycStatuses.FindAsync(sanctionedKycStatus.Id);
        updatedKycStatus!.RequiresEdd.Should().BeTrue();
        updatedKycStatus.EddReason.Should().Be("Sanctions");
    }

    #endregion

    #region RiskAssessmentWorker Tests

    [Fact]
    public async Task RiskAssessmentWorker_LowRiskClient_ShouldReturnLowRating()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var worker = new RiskAssessmentWorker(
            loggerFactory.CreateLogger<RiskAssessmentWorker>(),
            _context!);

        var mockJob = CreateMockJob(new Dictionary<string, object>
        {
            ["clientId"] = _testClientId.ToString(),
            ["documentComplete"] = true,
            ["amlRiskLevel"] = "Clear"
        });

        var mockJobClient = new Mock<IJobClient>();
        var completedVariables = new Dictionary<string, object>();

        mockJobClient.Setup(x => x.NewCompleteJobCommand(It.IsAny<long>()))
            .Returns(new MockCompleteJobCommand(completedVariables));

        // Act
        await worker.HandleJobAsync(mockJobClient.Object, mockJob.Object);

        // Assert
        completedVariables["riskScore"].Should().BeOfType<int>();
        var riskScore = (int)completedVariables["riskScore"];
        riskScore.Should().BeLessThanOrEqualTo(25); // Low risk threshold

        completedVariables["riskRating"].Should().Be("Low");
    }

    [Fact]
    public async Task RiskAssessmentWorker_HighAmlRisk_ShouldReturnHighRating()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var worker = new RiskAssessmentWorker(
            loggerFactory.CreateLogger<RiskAssessmentWorker>(),
            _context!);

        var mockJob = CreateMockJob(new Dictionary<string, object>
        {
            ["clientId"] = _testClientId.ToString(),
            ["documentComplete"] = true,
            ["amlRiskLevel"] = "High" // High AML risk
        });

        var mockJobClient = new Mock<IJobClient>();
        var completedVariables = new Dictionary<string, object>();

        mockJobClient.Setup(x => x.NewCompleteJobCommand(It.IsAny<long>()))
            .Returns(new MockCompleteJobCommand(completedVariables));

        // Act
        await worker.HandleJobAsync(mockJobClient.Object, mockJob.Object);

        // Assert
        completedVariables["riskScore"].Should().BeOfType<int>();
        var riskScore = (int)completedVariables["riskScore"];
        riskScore.Should().BeGreaterThan(50); // High risk threshold

        completedVariables["riskRating"].Should().Be("High");
    }

    #endregion

    #region AmlScreeningService Tests

    [Fact]
    public async Task ManualAmlScreeningService_CleanClient_ShouldReturnClearRisk()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var service = new ManualAmlScreeningService(_context!,
            loggerFactory.CreateLogger<ManualAmlScreeningService>());

        // Act
        var result = await service.PerformScreeningAsync(
            _testClientId, _testKycStatusId, "test-screener", "test-correlation");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.OverallRiskLevel.Should().Be("Clear");
        result.Value.SanctionsHit.Should().BeFalse();
        result.Value.PepMatch.Should().BeFalse();
        result.Value.Screenings.Should().HaveCount(2);

        // Verify database records
        var screenings = await _context!.AmlScreenings
            .Where(a => a.KycStatusId == _testKycStatusId)
            .ToListAsync();

        screenings.Should().HaveCount(2);
        screenings.Should().Contain(s => s.ScreeningType == "Sanctions");
        screenings.Should().Contain(s => s.ScreeningType == "PEP");
    }

    [Fact]
    public async Task ManualAmlScreeningService_SanctionedName_ShouldReturnHighRisk()
    {
        // Arrange - Create client with sanctioned name
        var sanctionedClient = new Client
        {
            Id = Guid.NewGuid(),
            Nrc = "333333/33/3",
            FirstName = "Vladimir",
            LastName = "Putin", // On sanctions list
            DateOfBirth = new DateTime(1952, 10, 7),
            Gender = "M",
            MaritalStatus = "Married",
            PrimaryPhone = "+260977333333",
            PhysicalAddress = "999 Test St",
            City = "Lusaka",
            Province = "Lusaka",
            BranchId = Guid.NewGuid(),
            CreatedBy = "test-user",
            UpdatedBy = "test-user"
        };

        _context!.Clients.Add(sanctionedClient);

        var sanctionedKyc = new KycStatus
        {
            Id = Guid.NewGuid(),
            ClientId = sanctionedClient.Id,
            CurrentState = KycState.InProgress,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.KycStatuses.Add(sanctionedKyc);
        await _context.SaveChangesAsync();

        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var service = new ManualAmlScreeningService(_context,
            loggerFactory.CreateLogger<ManualAmlScreeningService>());

        // Act
        var result = await service.PerformScreeningAsync(
            sanctionedClient.Id, sanctionedKyc.Id, "test-screener");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.OverallRiskLevel.Should().Be("High");
        result.Value.SanctionsHit.Should().BeTrue();

        // Verify match details recorded
        var sanctionsScreening = result.Value.Screenings
            .First(s => s.ScreeningType == "Sanctions");

        sanctionsScreening.IsMatch.Should().BeTrue();
        sanctionsScreening.MatchDetails.Should().NotBeNullOrEmpty();
        sanctionsScreening.MatchDetails.Should().Contain("Putin");
    }

    [Fact]
    public async Task ManualAmlScreeningService_PepName_ShouldReturnHighRisk()
    {
        // Arrange - Create client with PEP name
        var pepClient = new Client
        {
            Id = Guid.NewGuid(),
            Nrc = "444444/44/4",
            FirstName = "Political",
            LastName = "Figure", // On PEP list
            DateOfBirth = new DateTime(1970, 3, 20),
            Gender = "F",
            MaritalStatus = "Single",
            PrimaryPhone = "+260977444444",
            PhysicalAddress = "777 Test St",
            City = "Lusaka",
            Province = "Lusaka",
            BranchId = Guid.NewGuid(),
            CreatedBy = "test-user",
            UpdatedBy = "test-user"
        };

        _context!.Clients.Add(pepClient);

        var pepKyc = new KycStatus
        {
            Id = Guid.NewGuid(),
            ClientId = pepClient.Id,
            CurrentState = KycState.InProgress,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.KycStatuses.Add(pepKyc);
        await _context.SaveChangesAsync();

        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var service = new ManualAmlScreeningService(_context,
            loggerFactory.CreateLogger<ManualAmlScreeningService>());

        // Act
        var result = await service.PerformScreeningAsync(
            pepClient.Id, pepKyc.Id, "test-screener");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.OverallRiskLevel.Should().Be("High");
        result.Value.PepMatch.Should().BeTrue();

        // Verify match details
        var pepScreening = result.Value.Screenings
            .First(s => s.ScreeningType == "PEP");

        pepScreening.IsMatch.Should().BeTrue();
        pepScreening.MatchDetails.Should().Contain("Political Figure");
    }

    #endregion

    #region Helper Methods

    private async Task CreateVerifiedDocument(Guid clientId, string documentType)
    {
        var document = new ClientDocument
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            DocumentType = documentType,
            Category = "KYC",
            ObjectKey = $"test/{documentType}/{Guid.NewGuid()}",
            BucketName = "kyc-documents",
            FileName = $"{documentType}.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1024,
            FileHashSha256 = "testhash",
            UploadStatus = UploadStatus.Verified, // Only verified docs count
            UploadedBy = "user1",
            UploadedAt = DateTime.UtcNow,
            VerifiedBy = "user2",
            VerifiedAt = DateTime.UtcNow,
            RetentionUntil = DateTime.UtcNow.AddYears(7),
            CreatedAt = DateTime.UtcNow
        };

        _context!.ClientDocuments.Add(document);
        await _context.SaveChangesAsync();
    }

    private static Mock<IJob> CreateMockJob(Dictionary<string, object> variables)
    {
        var mockJob = new Mock<IJob>();
        mockJob.Setup(j => j.Key).Returns(123456);
        mockJob.Setup(j => j.Type).Returns("test-job-type");
        mockJob.Setup(j => j.ProcessInstanceKey).Returns(789);
        mockJob.Setup(j => j.Retries).Returns(3);
        mockJob.Setup(j => j.Variables).Returns(new MockVariables(variables));

        return mockJob;
    }

    private class MockVariables : Dictionary<string, object>
    {
        public MockVariables(Dictionary<string, object> variables) : base(variables)
        {
        }
    }

    private class MockCompleteJobCommand : ICompleteJobCommandStep1
    {
        private readonly Dictionary<string, object> _capturedVariables;

        public MockCompleteJobCommand(Dictionary<string, object> capturedVariables)
        {
            _capturedVariables = capturedVariables;
        }

        public ICompleteJobCommandStep2 Variables(string variables)
        {
            return this;
        }

        public ICompleteJobCommandStep2 Variables(object variables)
        {
            // Capture variables passed to Complete
            if (variables is Dictionary<string, object> dict)
            {
                foreach (var kvp in dict)
                {
                    _capturedVariables[kvp.Key] = kvp.Value;
                }
            }
            return this;
        }

        public async Task Send(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
        }
    }

    #endregion
}
