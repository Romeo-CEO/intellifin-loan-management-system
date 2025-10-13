using IntelliFin.AdminService.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio.DataModel.Args;

namespace IntelliFin.AdminService.Services;

public sealed class AuditArchiveReplicationMonitor : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AuditArchiveReplicationMonitor> _logger;
    private AuditArchiveOptions _options;

    public AuditArchiveReplicationMonitor(
        IServiceProvider serviceProvider,
        IOptionsMonitor<AuditArchiveOptions> optionsMonitor,
        ILogger<AuditArchiveReplicationMonitor> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = optionsMonitor.CurrentValue;
        optionsMonitor.OnChange(options => _options = options);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.ReplicationCheckIntervalMinutes <= 0)
        {
            _logger.LogWarning("Replication monitoring disabled because interval is not configured");
            return;
        }

        _logger.LogInformation("Audit archive replication monitor running every {Minutes} minutes", _options.ReplicationCheckIntervalMinutes);

        await CheckReplicationAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(_options.ReplicationCheckIntervalMinutes), stoppingToken);
                await CheckReplicationAsync(stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Replication monitor encountered an unexpected error");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }

    private async Task CheckReplicationAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var archiveService = scope.ServiceProvider.GetRequiredService<IAuditArchiveService>();
        var minioClient = scope.ServiceProvider.GetRequiredService<Minio.IMinioClient>();

        var pending = await archiveService.GetPendingReplicationAsync(cancellationToken);
        if (pending.Count == 0)
        {
            _logger.LogDebug("All audit archives replicated; nothing to update");
            return;
        }

        foreach (var archive in pending)
        {
            try
            {
                var stat = await minioClient.StatObjectAsync(new StatObjectArgs()
                    .WithBucket(_options.BucketName)
                    .WithObject(archive.ObjectKey), cancellationToken);

                await archiveService.UpdateReplicationStatusAsync(archive, stat.ReplicationStatus, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh replication status for archive {ArchiveId}", archive.ArchiveId);
            }
        }
    }
}
