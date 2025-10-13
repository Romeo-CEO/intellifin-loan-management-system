using IntelliFin.AdminService.Models;
using IntelliFin.AdminService.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntelliFin.AdminService.Services;

public sealed class AuditChainVerificationService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AuditChainVerificationService> _logger;
    private AuditChainOptions _options;

    public AuditChainVerificationService(
        IServiceProvider serviceProvider,
        IOptionsMonitor<AuditChainOptions> optionsMonitor,
        ILogger<AuditChainVerificationService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = optionsMonitor.CurrentValue;
        optionsMonitor.OnChange(updated => _options = updated);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Audit chain verification service running with interval {Interval} hours",
            _options.VerificationIntervalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunVerificationAsync(stoppingToken);

                var intervalHours = Math.Max(1, _options.VerificationIntervalHours);
                await Task.Delay(TimeSpan.FromHours(intervalHours), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing scheduled audit chain verification");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        _logger.LogInformation("Audit chain verification service stopped");
    }

    private async Task RunVerificationAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();

        _logger.LogInformation("Starting scheduled audit chain verification");
        var result = await auditService.VerifyChainIntegrityAsync(null, null, "System", cancellationToken);

        if (result.Status == ChainStatus.Valid)
        {
            _logger.LogInformation(
                "Scheduled verification succeeded. Events verified: {Count}. Duration: {Duration} ms",
                result.EventsVerified,
                result.DurationMs);
        }
        else
        {
            _logger.LogCritical(
                "Scheduled verification detected chain status {Status}. Broken event: {EventId}",
                result.Status,
                result.BrokenEventId);
        }
    }
}
