using FluentAssertions;
using IntelliFin.AdminService.Contracts.Requests;
using IntelliFin.AdminService.Data;
using IntelliFin.AdminService.Models;
using IntelliFin.AdminService.Options;
using IntelliFin.AdminService.Services;
using IntelliFin.Shared.Audit;
using IntelliFin.Shared.Camunda;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Moq;

namespace IntelliFin.Tests.Unit.Infrastructure.Security;

public class BastionAccessServiceTests
{
    private static BastionAccessService CreateService(
        AdminDbContext context,
        Mock<IMinioClient>? minioMock = null,
        BastionOptions? options = null)
    {
        options ??= new BastionOptions();

        var auditMock = new Mock<IAuditService>();
        auditMock
            .Setup(a => a.LogAsync(It.IsAny<AuditEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var camundaMock = new Mock<ICamundaClient>();
        camundaMock
            .Setup(c => c.StartProcessAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("process-123");

        minioMock ??= new Mock<IMinioClient>();
        minioMock
            .Setup(m => m.PresignedGetObjectAsync(It.IsAny<PresignedGetObjectArgs>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://download.example/sessions/1");

        var optionsMonitor = Mock.Of<IOptionsMonitor<BastionOptions>>(m => m.CurrentValue == options);
        var logger = Mock.Of<ILogger<BastionAccessService>>();

        return new BastionAccessService(
            context,
            auditMock.Object,
            camundaMock.Object,
            minioMock.Object,
            optionsMonitor,
            logger);
    }

    private static AdminDbContext CreateDbContext(string name)
    {
        var options = new DbContextOptionsBuilder<AdminDbContext>()
            .UseInMemoryDatabase(name)
            .Options;

        var context = new AdminDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task RequestAccessAsync_NonProductionAutoApproves()
    {
        using var context = CreateDbContext(nameof(RequestAccessAsync_NonProductionAutoApproves));
        var service = CreateService(context);

        var input = new BastionAccessRequestInput
        {
            Environment = "staging",
            AccessDurationHours = 2,
            Justification = new string('A', 60)
        };

        var result = await service.RequestAccessAsync(input, "user-1", "User One", "user.one@example.com", CancellationToken.None);

        result.RequiresApproval.Should().BeFalse();
        result.Status.Should().Be("Approved");
        result.CertificateContent.Should().NotBeNull();
        result.BastionHost.Should().Be("bastion.intellifin.local");

        var stored = await context.BastionAccessRequests.SingleAsync();
        stored.SshCertificateIssued.Should().BeTrue();
    }

    [Fact]
    public async Task RequestAccessAsync_ProductionRequiresApproval()
    {
        using var context = CreateDbContext(nameof(RequestAccessAsync_ProductionRequiresApproval));
        var service = CreateService(context);

        var input = new BastionAccessRequestInput
        {
            Environment = "production",
            AccessDurationHours = 4,
            Justification = new string('B', 80)
        };

        var result = await service.RequestAccessAsync(input, "user-2", "Prod Admin", "prod.admin@example.com", CancellationToken.None);

        result.RequiresApproval.Should().BeTrue();
        result.Status.Should().Be("Pending");
        result.CertificateContent.Should().BeNull();
    }

    [Fact]
    public async Task GetSessionRecordingAsync_ReturnsPresignedUrl()
    {
        using var context = CreateDbContext(nameof(GetSessionRecordingAsync_ReturnsPresignedUrl));
        var request = new BastionAccessRequest
        {
            RequestId = Guid.NewGuid(),
            UserId = "user-3",
            UserName = "Alice",
            UserEmail = "alice@example.com",
            Environment = "dev",
            AccessDurationHours = 2,
            Justification = new string('C', 60),
            Status = "Approved",
            RequiresApproval = false,
            SshCertificateIssued = true,
            CertificateExpiresAt = DateTime.UtcNow.AddHours(2)
        };
        context.BastionAccessRequests.Add(request);
        context.BastionSessions.Add(new BastionSession
        {
            SessionId = Guid.NewGuid(),
            AccessRequestId = request.RequestId,
            Username = "alice",
            ClientIp = "10.0.0.5",
            BastionHost = "bastion.dev.intellifin.local",
            RecordingPath = "dev/alice/session.cast",
            Status = "Completed",
            StartTime = DateTime.UtcNow.AddMinutes(-10),
            EndTime = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var minioMock = new Mock<IMinioClient>();
        minioMock
            .Setup(m => m.PresignedGetObjectAsync(It.IsAny<PresignedGetObjectArgs>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://download.example/session.cast");

        var service = CreateService(context, minioMock, new BastionOptions
        {
            SessionBucketName = "bastion-sessions",
            SessionPrefix = "dev"
        });

        var session = await context.BastionSessions.AsNoTracking().FirstAsync();
        var recording = await service.GetSessionRecordingAsync(session.SessionId.ToString(), CancellationToken.None);

        recording.Should().NotBeNull();
        recording!.DownloadUrl.Should().Be("https://download.example/session.cast");
        recording.Username.Should().Be("alice");
    }

    [Fact]
    public async Task RecordSessionAsync_NewSession_PersistsSession()
    {
        using var context = CreateDbContext(nameof(RecordSessionAsync_NewSession_PersistsSession));
        var accessRequest = new BastionAccessRequest
        {
            RequestId = Guid.NewGuid(),
            UserId = "alice",
            UserName = "alice",
            UserEmail = "alice@example.com",
            Environment = "dev",
            Status = "Approved",
            RequiresApproval = false,
            SshCertificateIssued = true,
            CertificateExpiresAt = DateTime.UtcNow.AddHours(2)
        };
        context.BastionAccessRequests.Add(accessRequest);
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var ingest = new BastionSessionIngestRequest
        {
            SessionId = Guid.NewGuid(),
            AccessRequestId = accessRequest.RequestId,
            Username = "alice",
            ClientIp = "10.0.0.5",
            BastionHost = "bastion.dev.local",
            RecordingPath = "dev/alice/session.cast",
            StartTime = DateTime.UtcNow.AddMinutes(-5),
            EndTime = DateTime.UtcNow,
            CommandCount = 12
        };

        await service.RecordSessionAsync(ingest, CancellationToken.None);

        var stored = await context.BastionSessions.SingleAsync();
        stored.AccessRequestId.Should().Be(accessRequest.RequestId);
        stored.CommandCount.Should().Be(12);
        stored.Status.Should().Be("Completed");
        stored.RecordingPath.Should().Be("dev/alice/session.cast");
    }

    [Fact]
    public async Task RecordSessionAsync_UpdatesExistingSession()
    {
        using var context = CreateDbContext(nameof(RecordSessionAsync_UpdatesExistingSession));
        var sessionId = Guid.NewGuid();
        context.BastionSessions.Add(new BastionSession
        {
            SessionId = sessionId,
            Username = "bob",
            ClientIp = "10.0.0.6",
            BastionHost = "bastion.dev.local",
            RecordingPath = "dev/bob/initial.cast",
            Status = "Active",
            StartTime = DateTime.UtcNow.AddMinutes(-15)
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var ingest = new BastionSessionIngestRequest
        {
            SessionId = sessionId,
            Username = "bob",
            ClientIp = "10.0.0.6",
            BastionHost = "bastion.dev.local",
            RecordingPath = "dev/bob/final.cast",
            EndTime = DateTime.UtcNow,
            Status = "Completed",
            CommandCount = 20
        };

        await service.RecordSessionAsync(ingest, CancellationToken.None);

        var stored = await context.BastionSessions.SingleAsync();
        stored.Status.Should().Be("Completed");
        stored.CommandCount.Should().Be(20);
        stored.RecordingPath.Should().Be("dev/bob/final.cast");
        stored.EndTime.Should().NotBeNull();
        stored.DurationSeconds.Should().NotBeNull();
    }
}
