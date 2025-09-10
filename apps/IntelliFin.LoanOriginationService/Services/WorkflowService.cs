using IntelliFin.LoanOriginationService.Models;
using System.Text.Json;
using Zeebe.Client;
using Zeebe.Client.Api.Responses;

namespace IntelliFin.LoanOriginationService.Services;

public class WorkflowService : IWorkflowService
{
    private readonly ILogger<WorkflowService> _logger;
    private readonly IZeebeClient _zeebeClient;

    public WorkflowService(ILogger<WorkflowService> logger, IZeebeClient zeebeClient)
    {
        _logger = logger;
        _zeebeClient = zeebeClient;
    }

    public async Task<string> StartApprovalWorkflowAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting Camunda workflow for application {ApplicationId}", applicationId);

            // In a real scenario, you would pass variables needed by the workflow
            // For example: .Variables(new { applicationId = applicationId.ToString(), loanAmount = 50000 });

            var processInstance = await _zeebeClient.NewCreateProcessInstanceCommand()
                .BpmnProcessId("loanOriginationProcess") // This MUST match the ID in your BPMN file
                .LatestVersion()
                .Variables(JsonSerializer.Serialize(new { applicationId = applicationId.ToString() }))
                .WithResult()
                .Send(cancellationToken);

            _logger.LogInformation("Successfully started process instance {ProcessInstanceKey} for application {ApplicationId}", 
                processInstance.ProcessInstanceKey, applicationId);

            return processInstance.ProcessInstanceKey.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting Camunda workflow for application {ApplicationId}", applicationId);
            // Depending on business requirements, you might want to re-throw or handle this differently
            throw;
        }
    }

    [Obsolete("User tasks are now managed via the Camunda Tasklist API/UI. This method is no longer used.")]
    public Task<bool> CompleteWorkflowTaskAsync(string taskId, WorkflowDecision decision, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("CompleteWorkflowTaskAsync is obsolete and should not be called.");
        return Task.FromResult(false);
    }

    [Obsolete("Workflow history is now viewed in Camunda Operate. Querying history requires the Operate API, not the Zeebe client.")]
    public Task<List<WorkflowStep>> GetWorkflowStepsAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("GetWorkflowStepsAsync is obsolete and should not be called.");
        return Task.FromResult(new List<WorkflowStep>());
    }

    [Obsolete("The concept of a single 'current step' is managed by Camunda. Active user tasks are found via the Tasklist API.")]
    public Task<string?> GetCurrentWorkflowStepAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("GetCurrentWorkflowStepAsync is obsolete and should not be called.");
        return Task.FromResult<string?>(null);
    }

    [Obsolete("Task reassignment is now managed via the Camunda Tasklist API/UI.")]
    public Task<bool> ReassignWorkflowTaskAsync(string taskId, string newAssignee, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("ReassignWorkflowTaskAsync is obsolete and should not be called.");
        return Task.FromResult(false);
    }

    [Obsolete("This method is no longer applicable as the workflow is driven by events and workers in Camunda.")]
    public Task<bool> AdvanceWorkflowAsync(Guid applicationId, string nextStep, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("AdvanceWorkflowAsync is obsolete and should not be called.");
        return Task.FromResult(false);
    }
}
