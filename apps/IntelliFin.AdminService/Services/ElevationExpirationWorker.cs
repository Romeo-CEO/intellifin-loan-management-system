using System;
using System.Threading;
using System.Threading.Tasks;
using IntelliFin.AdminService.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntelliFin.AdminService.Services;

public sealed class ElevationExpirationWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptionsMonitor<ElevationOptions> _optionsMonitor;
    private readonly ILogger<ElevationExpirationWorker> _logger;

    public ElevationExpirationWorker(
        IServiceProvider serviceProvider,
        IOptionsMonitor<ElevationOptions> optionsMonitor,
        ILogger<ElevationExpirationWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(Math.Max(1, _optionsMonitor.CurrentValue.ExpirationCheckIntervalMinutes)), stoppingToken);
            }
            catch (TaskCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var elevationService = scope.ServiceProvider.GetRequiredService<IAccessElevationService>();
                var expired = await elevationService.ExpireElevationsAsync(stoppingToken);
                if (expired > 0)
                {
                    _logger.LogInformation("Elevation expiration worker processed {Count} records", expired);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Failed to process elevation expirations");
            }
        }
    }
}
