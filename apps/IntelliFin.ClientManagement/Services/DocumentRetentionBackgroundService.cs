using Microsoft.Extensions.DependencyInjection;

namespace IntelliFin.ClientManagement.Services;

/// <summary>
/// Background service for automated document retention and archival
/// Runs daily to archive documents that have exceeded 10-year retention
/// </summary>
public class DocumentRetentionBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DocumentRetentionBackgroundService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(24); // Run daily

    public DocumentRetentionBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<DocumentRetentionBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Document Retention Background Service started. Will run every {Interval} hours",
            _interval.TotalHours);

        // Wait 1 minute after startup before first run
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Starting automated document archival cycle");

                using var scope = _serviceProvider.CreateScope();
                var retentionService = scope.ServiceProvider
                    .GetRequiredService<IDocumentRetentionService>();

                var result = await retentionService.ArchiveExpiredDocumentsAsync(stoppingToken);

                if (result.IsSuccess)
                {
                    _logger.LogInformation(
                        "Archival cycle completed successfully. Archived {Count} documents",
                        result.Value);
                }
                else
                {
                    _logger.LogError(
                        "Archival cycle failed: {Error}",
                        result.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in document retention background service");
            }

            // Wait for next cycle
            _logger.LogInformation(
                "Next archival cycle scheduled in {Hours} hours",
                _interval.TotalHours);

            await Task.Delay(_interval, stoppingToken);
        }

        _logger.LogInformation("Document Retention Background Service stopped");
    }
}
