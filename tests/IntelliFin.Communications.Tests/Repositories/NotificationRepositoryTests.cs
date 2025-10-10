using System.Linq;
using IntelliFin.Communications.Services;
using IntelliFin.Shared.DomainModels.Data;
using IntelliFin.Shared.DomainModels.Entities;
using IntelliFin.Shared.DomainModels.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace IntelliFin.Communications.Tests.Repositories;

public class NotificationRepositoryTests
{
    private static LmsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<LmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new LmsDbContext(options);
    }

    [Fact]
    public async Task CreateAsync_ShouldPersistNotificationLog()
    {
        await using var context = CreateContext();
        INotificationRepository repository = new NotificationRepository(context, NullLogger<NotificationRepository>.Instance);

        var log = new NotificationLog
        {
            EventId = Guid.NewGuid(),
            RecipientId = "customer-1",
            RecipientType = "Customer",
            Channel = "SMS",
            Content = "Test message",
            BranchId = 1,
            CreatedBy = "tests"
        };

        var saved = await repository.CreateAsync(log);
        var fetched = await repository.GetByIdAsync(saved.Id);

        Assert.NotNull(fetched);
        Assert.Equal("customer-1", fetched!.RecipientId);
        Assert.Equal(NotificationStatus.Pending, fetched.Status);
    }

    [Fact]
    public async Task MarkEventProcessedAsync_ShouldRecordAndReturnStatus()
    {
        await using var context = CreateContext();
        INotificationRepository repository = new NotificationRepository(context, NullLogger<NotificationRepository>.Instance);

        var eventId = Guid.NewGuid();

        var processedBefore = await repository.IsEventProcessedAsync(eventId);
        Assert.False(processedBefore);

        await repository.MarkEventProcessedAsync(eventId, "LoanApplicationCreated", success: true);

        var processedAfter = await repository.IsEventProcessedAsync(eventId);
        Assert.True(processedAfter);
    }

    [Fact]
    public async Task GetStatsAsync_ShouldAggregateNotificationData()
    {
        await using var context = CreateContext();
        INotificationRepository repository = new NotificationRepository(context, NullLogger<NotificationRepository>.Instance);

        var logs = Enumerable.Range(0, 5).Select(i => new NotificationLog
        {
            EventId = Guid.NewGuid(),
            RecipientId = $"customer-{i}",
            RecipientType = "Customer",
            Channel = i % 2 == 0 ? "SMS" : "InApp",
            Content = "message",
            Status = i % 2 == 0 ? NotificationStatus.Sent : NotificationStatus.Failed,
            BranchId = 1,
            CreatedBy = "tests",
            Cost = 0.5m
        }).ToList();

        await repository.CreateBulkAsync(logs);

        var stats = await repository.GetStatsAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));

        Assert.Equal(5, stats.StatusDistribution.Values.Sum());
        Assert.True(stats.ChannelUsage.ContainsKey("SMS"));
        Assert.True(stats.ChannelUsage.ContainsKey("InApp"));
    }
}
