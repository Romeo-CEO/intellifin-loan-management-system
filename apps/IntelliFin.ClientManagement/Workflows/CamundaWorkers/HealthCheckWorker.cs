using IntelliFin.ClientManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Zeebe.Client.Api.Responses;
using Zeebe.Client.Api.Worker;

namespace IntelliFin.ClientManagement.Workflows.CamundaWorkers;

/// <summary>
/// Example health check worker to validate Camunda infrastructure
/// Verifies database connectivity and returns health status
/// </summary>
public class HealthCheckWorker : ICamundaJobHandler
{
    private readonly ILogger<HealthCheckWorker> _logger;
    private readonly ClientManagementDbContext _context;

    public HealthCheckWorker(
        ILogger<HealthCheckWorker> logger,
        ClientManagementDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task HandleJobAsync(IJobClient jobClient, IJob job)
    {
        _logger.LogInformation(
            "Health check worker processing job {JobKey}",
            job.Key);

        var healthStatus = new
        {
            ServiceName = "IntelliFin.ClientManagement",
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Checks = new Dictionary<string, string>()
        };

        try
        {
            // Check database connectivity
            var canConnect = await _context.Database.CanConnectAsync();
            healthStatus.Checks["Database"] = canConnect ? "Healthy" : "Unhealthy";

            _logger.LogInformation(
                "Health check completed: Database={DatabaseStatus}",
                healthStatus.Checks["Database"]);

            // Complete job with health status
            await jobClient.NewCompleteJobCommand(job.Key)
                .Variables(healthStatus)
                .Send();

            _logger.LogInformation(
                "Health check job {JobKey} completed successfully",
                job.Key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Health check failed for job {JobKey}",
                job.Key);

            // Fail job to trigger retry
            await jobClient.NewFailCommand(job.Key)
                .Retries(job.Retries - 1)
                .ErrorMessage($"Health check failed: {ex.Message}")
                .Send();
        }
    }

    public string GetTopicName() => "client.health.check";

    public string GetJobType() => "io.intellifin.health.check";
}
