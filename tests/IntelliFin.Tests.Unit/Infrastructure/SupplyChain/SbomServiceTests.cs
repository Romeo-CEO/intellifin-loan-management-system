using IntelliFin.AdminService.Contracts.Requests;
using IntelliFin.AdminService.Data;
using IntelliFin.AdminService.Models;
using IntelliFin.AdminService.Options;
using IntelliFin.AdminService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Moq;

namespace IntelliFin.Tests.Unit.Infrastructure.SupplyChain;

public class SbomServiceTests
{
    private static SbomService CreateService(AdminDbContext context, SbomOptions? options = null, Mock<IMinioClient>? minioMock = null)
    {
        options ??= new SbomOptions();
        var optionsMonitor = Mock.Of<IOptionsMonitor<SbomOptions>>(m => m.CurrentValue == options);
        minioMock ??= new Mock<IMinioClient>(MockBehavior.Strict);
        var logger = Mock.Of<ILogger<SbomService>>();
        return new SbomService(context, minioMock.Object, optionsMonitor, logger);
    }

    private static AdminDbContext CreateDbContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<AdminDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        var context = new AdminDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task ListSbomsAsync_ReturnsOrderedResults()
    {
        using var context = CreateDbContext(nameof(ListSbomsAsync_ReturnsOrderedResults));
        context.ContainerImages.AddRange(
            new ContainerImage
            {
                ServiceName = "identity-service",
                Version = "1.0.0",
                ImageDigest = "sha256:a",
                Registry = "ghcr.io/intellifin/identity-service",
                BuildTimestamp = DateTime.UtcNow.AddHours(-2),
                IsSigned = true,
                SignatureVerified = true,
                HasSbom = true,
                CriticalCount = 1,
                CreatedAt = DateTime.UtcNow
            },
            new ContainerImage
            {
                ServiceName = "identity-service",
                Version = "1.1.0",
                ImageDigest = "sha256:b",
                Registry = "ghcr.io/intellifin/identity-service",
                BuildTimestamp = DateTime.UtcNow,
                IsSigned = true,
                SignatureVerified = false,
                HasSbom = false,
                CriticalCount = 0,
                CreatedAt = DateTime.UtcNow
            });
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var result = await service.ListSBOMsAsync("identity-service", 1, 10, CancellationToken.None);

        result.Items.Should().HaveCount(2);
        result.Items.First().Version.Should().Be("1.1.0");
    }

    [Fact]
    public async Task GetVulnerabilityStatisticsAsync_AggregatesCounts()
    {
        using var context = CreateDbContext(nameof(GetVulnerabilityStatisticsAsync_AggregatesCounts));
        var image = new ContainerImage
        {
            ServiceName = "loan-service",
            Version = "2.0.0",
            ImageDigest = "sha256:c",
            Registry = "ghcr.io/intellifin/loan-service",
            BuildTimestamp = DateTime.UtcNow,
            IsSigned = true,
            SignatureVerified = true,
            HasSbom = true,
            CriticalCount = 2,
            HighCount = 3,
            MediumCount = 1,
            LowCount = 0,
            CreatedAt = DateTime.UtcNow
        };
        image.Vulnerabilities.Add(new Vulnerability
        {
            VulnerabilityId = "CVE-123",
            PackageName = "Sample.Library",
            Severity = "CRITICAL"
        });
        image.Vulnerabilities.Add(new Vulnerability
        {
            VulnerabilityId = "CVE-456",
            PackageName = "Sample.Library",
            Severity = "HIGH"
        });
        context.ContainerImages.Add(image);
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var stats = await service.GetVulnerabilityStatisticsAsync(CancellationToken.None);

        stats.TotalImages.Should().Be(1);
        stats.SignedImages.Should().Be(1);
        stats.ImagesWithSbom.Should().Be(1);
        stats.CriticalCount.Should().Be(2);
        stats.TopPackages.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GenerateComplianceReportAsync_FiltersByPeriod()
    {
        using var context = CreateDbContext(nameof(GenerateComplianceReportAsync_FiltersByPeriod));
        context.ContainerImages.Add(new ContainerImage
        {
            ServiceName = "admin-service",
            Version = "3.0.0",
            ImageDigest = "sha256:d",
            Registry = "ghcr.io/intellifin/admin-service",
            BuildTimestamp = new DateTime(2025, 01, 10, 0, 0, 0, DateTimeKind.Utc),
            DeployedToProduction = true,
            DeploymentTimestamp = new DateTime(2025, 01, 15, 0, 0, 0, DateTimeKind.Utc),
            IsSigned = true,
            SignatureVerified = true,
            HasSbom = true,
            CriticalCount = 0,
            HighCount = 1,
            MediumCount = 0,
            LowCount = 0,
            CreatedAt = DateTime.UtcNow
        });
        context.SignatureVerificationAudits.Add(new SignatureVerificationAudit
        {
            ImageDigest = "sha256:d",
            ServiceName = "admin-service",
            Version = "3.0.0",
            VerificationMethod = "Cosign",
            VerificationResult = "Success",
            VerificationTimestamp = new DateTime(2025, 01, 15, 0, 0, 0, DateTimeKind.Utc)
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var request = new ComplianceReportRequest
        {
            PeriodStart = new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            PeriodEnd = new DateTime(2025, 01, 31, 0, 0, 0, DateTimeKind.Utc)
        };

        var report = await service.GenerateComplianceReportAsync(request, CancellationToken.None);

        report.Images.Should().HaveCount(1);
        report.Images.Single().ServiceName.Should().Be("admin-service");
        report.SignatureAuditTrail.Should().HaveCount(1);
        report.TotalImageCount.Should().Be(1);
    }
}
