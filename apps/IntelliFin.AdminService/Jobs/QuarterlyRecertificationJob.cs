using System;
using System.Threading.Tasks;
using IntelliFin.AdminService.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace IntelliFin.AdminService.Jobs;

[DisallowConcurrentExecution]
public sealed class QuarterlyRecertificationJob : IJob
{
    private readonly IRecertificationService _recertificationService;
    private readonly ILogger<QuarterlyRecertificationJob> _logger;

    public QuarterlyRecertificationJob(IRecertificationService recertificationService, ILogger<QuarterlyRecertificationJob> logger)
    {
        _recertificationService = recertificationService;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var now = DateTime.UtcNow;
        var quarter = (now.Month - 1) / 3 + 1;
        var quarterStartMonth = ((quarter - 1) * 3) + 1;
        var startDate = new DateTime(now.Year, quarterStartMonth, 1, 0, 0, 0, DateTimeKind.Utc);
        var dueDate = startDate.AddDays(30);

        var campaignId = $"RECERT-{now.Year}-Q{quarter}";
        var campaignName = $"Q{quarter} {now.Year} Access Recertification";

        _logger.LogInformation("Launching quarterly recertification campaign {CampaignId}", campaignId);

        await _recertificationService.CreateCampaignAsync(
            campaignId,
            campaignName,
            quarter,
            now.Year,
            startDate,
            dueDate,
            context.CancellationToken);
    }
}

public static class QuartzRecertificationExtensions
{
    public static void ConfigureRecertification(this IServiceCollectionQuartzConfigurator quartz)
    {
        var campaignJobKey = new JobKey("QuarterlyRecertificationJob");
        var monitorJobKey = new JobKey("RecertificationTaskMonitorJob");

        quartz.AddJob<QuarterlyRecertificationJob>(opts => opts.WithIdentity(campaignJobKey));
        quartz.AddJob<RecertificationTaskMonitorJob>(opts => opts.WithIdentity(monitorJobKey));

        quartz.AddTrigger(trigger => trigger
            .ForJob(campaignJobKey)
            .WithIdentity("QuarterlyRecertificationTrigger")
            .WithCronSchedule("0 0 0 1 1,4,7,10 ?")
            .WithDescription("Quarterly access recertification campaign"));

        quartz.AddTrigger(trigger => trigger
            .ForJob(monitorJobKey)
            .WithIdentity("RecertificationTaskMonitorTrigger")
            .WithCronSchedule("0 0 9 * * ?")
            .WithDescription("Daily recertification reminder processing"));
    }
}
