using System;
using System.Threading.Tasks;
using IntelliFin.AdminService.Services;
using Microsoft.Extensions.Logging;
using Quartz;

namespace IntelliFin.AdminService.Jobs;

[DisallowConcurrentExecution]
public sealed class RecertificationTaskMonitorJob : IJob
{
    private readonly IRecertificationService _recertificationService;
    private readonly ILogger<RecertificationTaskMonitorJob> _logger;

    public RecertificationTaskMonitorJob(
        IRecertificationService recertificationService,
        ILogger<RecertificationTaskMonitorJob> logger)
    {
        _recertificationService = recertificationService;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var utcNow = DateTime.UtcNow;
        var actions = await _recertificationService.ProcessTaskRemindersAsync(utcNow, context.CancellationToken);

        if (actions > 0)
        {
            _logger.LogInformation(
                "Processed {Count} recertification reminders or escalations at {Timestamp}",
                actions,
                utcNow);
        }
        else
        {
            _logger.LogDebug(
                "No recertification reminders due at {Timestamp}",
                utcNow);
        }
    }
}
