using Zeebe.Client;

namespace IntelliFin.LoanOriginationService.Services;

/// <summary>
/// Background service that deploys BPMN workflows to Zeebe/Camunda on application startup.
/// </summary>
/// <remarks>
/// This service implements IHostedService to ensure BPMN workflows are deployed when the application starts.
/// If deployment fails, the service will fail-fast and prevent application startup to ensure the system
/// does not operate without the required workflow definitions.
/// </remarks>
public class BpmnDeploymentService : IHostedService
{
    private readonly IZeebeClient _zeebeClient;
    private readonly ILogger<BpmnDeploymentService> _logger;
    private readonly IConfiguration _configuration;

    public BpmnDeploymentService(
        IZeebeClient zeebeClient,
        ILogger<BpmnDeploymentService> logger,
        IConfiguration configuration)
    {
        _zeebeClient = zeebeClient;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Starts the service and deploys BPMN workflows to Zeebe.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the asynchronous operation</returns>
    /// <exception cref="InvalidOperationException">Thrown when BPMN deployment fails</exception>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting BPMN workflow deployment to Zeebe");

            // Read BPMN file from Workflows folder
            var bpmnPath = Path.Combine(AppContext.BaseDirectory, "Workflows", "loan-origination-process.bpmn");
            
            if (!File.Exists(bpmnPath))
            {
                throw new FileNotFoundException($"BPMN workflow file not found at path: {bpmnPath}");
            }

            var bpmnContent = await File.ReadAllTextAsync(bpmnPath, cancellationToken);
            _logger.LogDebug("BPMN file loaded from {BpmnPath}, size: {Size} bytes", bpmnPath, bpmnContent.Length);

            // Deploy to Zeebe
            var deployResponse = await _zeebeClient.NewDeployCommand()
                .AddResourceStringUtf8(bpmnContent, "loan-origination-process.bpmn")
                .Send(cancellationToken);

            // Verify deployment success and log workflow details
            var workflow = deployResponse.Workflows.FirstOrDefault();
            if (workflow != null)
            {
                _logger.LogInformation(
                    "Successfully deployed BPMN workflow. ProcessKey: {ProcessKey}, BpmnProcessId: {BpmnProcessId}, Version: {Version}, ResourceName: {ResourceName}",
                    workflow.WorkflowKey, 
                    workflow.BpmnProcessId, 
                    workflow.Version,
                    workflow.ResourceName);
            }
            else
            {
                throw new InvalidOperationException("BPMN deployment failed - no workflow returned from Zeebe");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deploy BPMN workflow on startup. Application will not start.");
            // Fail fast - service should not start without workflow
            throw;
        }
    }

    /// <summary>
    /// Stops the service. No cleanup required for BPMN deployment.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Completed task</returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("BpmnDeploymentService stopping");
        return Task.CompletedTask;
    }
}
