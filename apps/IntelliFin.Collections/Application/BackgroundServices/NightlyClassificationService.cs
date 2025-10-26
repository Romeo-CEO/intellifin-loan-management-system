using IntelliFin.Collections.Application.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IntelliFin.Collections.Application.BackgroundServices;

/// <summary>
/// Background service that runs nightly arrears classification.
/// In production, this would be replaced by a Camunda workflow or scheduled job.
/// </summary>
public class NightlyClassificationService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NightlyClassificationService> _logger;
    private readonly TimeSpan _classificationTime = new(2, 0, 0); // 2:00 AM
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(30);

    public NightlyClassificationService(
        IServiceProvider serviceProvider,
        ILogger<NightlyClassificationService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Nightly Classification Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow.TimeOfDay;
                var timeUntilClassification = CalculateTimeUntilNext(_classificationTime, now);

                _logger.LogInformation(
                    "Next classification run scheduled in {Minutes} minutes",
                    timeUntilClassification.TotalMinutes);

                await Task.Delay(timeUntilClassification, stoppingToken);

                if (!stoppingToken.IsCancellationRequested)
                {
                    await RunClassificationAsync(stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error in Nightly Classification Service");
                await Task.Delay(_checkInterval, stoppingToken);
            }
        }

        _logger.LogInformation("Nightly Classification Service stopped");
    }

    private async Task RunClassificationAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var classificationService = scope.ServiceProvider
            .GetRequiredService<IArrearsClassificationService>();

        _logger.LogInformation("Starting nightly arrears classification");
        var startTime = DateTime.UtcNow;

        try
        {
            var count = await classificationService.ClassifyAllLoansAsync(cancellationToken);
            var duration = DateTime.UtcNow - startTime;

            _logger.LogInformation(
                "Nightly classification completed. Processed {Count} loans in {Duration}ms",
                count, duration.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Nightly classification failed");
        }
    }

    private static TimeSpan CalculateTimeUntilNext(TimeSpan targetTime, TimeSpan currentTime)
    {
        if (currentTime < targetTime)
        {
            return targetTime - currentTime;
        }
        else
        {
            // Schedule for tomorrow
            return TimeSpan.FromDays(1) - currentTime + targetTime;
        }
    }
}
