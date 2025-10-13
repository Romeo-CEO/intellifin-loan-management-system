using IntelliFin.AdminService.Options;
using Microsoft.Extensions.Options;

namespace IntelliFin.AdminService.Services;

public sealed class AuditBufferFlushService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AuditBufferFlushService> _logger;
    private readonly AuditIngestionOptions _options;

    public AuditBufferFlushService(
        IServiceProvider serviceProvider,
        IOptionsMonitor<AuditIngestionOptions> options,
        ILogger<AuditBufferFlushService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.CurrentValue;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Audit buffer flush service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_options.FlushIntervalSeconds), stoppingToken);

                using var scope = _serviceProvider.CreateScope();
                var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();
                await auditService.FlushBufferAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // normal during shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to flush audit buffer");
            }
        }

        _logger.LogInformation("Audit buffer flush service stopped");
    }
}
