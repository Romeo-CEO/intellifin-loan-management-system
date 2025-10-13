using IntelliFin.AdminService.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntelliFin.AdminService.Services;

public sealed class AuditArchiveExportWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AuditArchiveExportWorker> _logger;
    private AuditArchiveOptions _options;

    public AuditArchiveExportWorker(
        IServiceProvider serviceProvider,
        IOptionsMonitor<AuditArchiveOptions> optionsMonitor,
        ILogger<AuditArchiveExportWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = optionsMonitor.CurrentValue;
        optionsMonitor.OnChange(options => _options = options);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Audit archive export worker started with schedule {Hour:D2}:{Minute:D2} UTC", _options.ExportHourUtc, _options.ExportMinuteUtc);

        await AttemptExportAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var nextRun = GetNextRunUtc(DateTime.UtcNow, _options);
                var delay = nextRun - DateTime.UtcNow;
                if (delay > TimeSpan.Zero)
                {
                    _logger.LogDebug("Next audit archive export scheduled for {NextRun:o} (sleeping {Delay})", nextRun, delay);
                    await Task.Delay(delay, stoppingToken);
                }

                await AttemptExportAsync(stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // application shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in audit archive export worker");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }

    private async Task AttemptExportAsync(CancellationToken cancellationToken)
    {
        if (!_options.EnableExports)
        {
            _logger.LogDebug("Audit archive exports disabled; skipping export attempt");
            return;
        }

        var exportDate = DateTime.UtcNow.Date.AddDays(-1);

        using var scope = _serviceProvider.CreateScope();
        var archiveService = scope.ServiceProvider.GetRequiredService<IAuditArchiveService>();

        if (await archiveService.ArchiveExistsAsync(exportDate, cancellationToken))
        {
            _logger.LogDebug("Archive for {ExportDate:yyyy-MM-dd} already exists", exportDate);
            return;
        }

        _logger.LogInformation("Starting scheduled audit archive export for {ExportDate:yyyy-MM-dd}", exportDate);
        var result = await archiveService.ExportDailyAuditEventsAsync(exportDate, cancellationToken);

        if (!result.Success)
        {
            _logger.LogError("Audit archive export for {ExportDate:yyyy-MM-dd} failed: {Message}", exportDate, result.ErrorMessage);
        }
    }

    private static DateTime GetNextRunUtc(DateTime referenceUtc, AuditArchiveOptions options)
    {
        var scheduled = new DateTime(referenceUtc.Year, referenceUtc.Month, referenceUtc.Day, options.ExportHourUtc, options.ExportMinuteUtc, 0, DateTimeKind.Utc);
        return referenceUtc >= scheduled ? scheduled.AddDays(1) : scheduled;
    }
}
