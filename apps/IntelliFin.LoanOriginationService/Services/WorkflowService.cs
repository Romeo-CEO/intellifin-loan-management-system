using IntelliFin.LoanOriginationService.Models;
using System.Net.Http.Json;

namespace IntelliFin.LoanOriginationService.Services;

public class WorkflowService : IWorkflowService
{
    private readonly ILogger<WorkflowService> _logger;
    private readonly HttpClient _camundaClient;

    public WorkflowService(ILogger<WorkflowService> logger, HttpClient httpClient)
    {
        _logger = logger;
        _camundaClient = httpClient;

        if (_camundaClient.BaseAddress == null)
        {
            _camundaClient.BaseAddress = new Uri("http://localhost:8080/engine-rest/");
        }
    }

    public async Task<string> StartApprovalWorkflowAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        return await StartApprovalWorkflowAsync(applicationId, "UNKNOWN", 0, cancellationToken);
    }

    public async Task<string> StartApprovalWorkflowAsync(Guid applicationId, string productType, decimal loanAmount, CancellationToken cancellationToken = default)
    {
        var camundaRequest = new CamundaProcessInstance
        {
            ProcessDefinitionKey = "loanOriginationProcess",
            BusinessKey = applicationId.ToString(),
            Variables = new Dictionary<string, object>
            {
                ["applicationId"] = new { value = applicationId.ToString(), type = "String" },
                ["productType"] = new { value = productType, type = "String" },
                ["loanAmount"] = new { value = loanAmount, type = "Double" }
            }
        };

        var processInstance = await StartCamundaProcessAsync(camundaRequest, cancellationToken);
        return processInstance?.Id ?? string.Empty;
    }

    [Obsolete("Human task completion is now handled directly by Camunda Tasklist API. This method is deprecated.")]
    public async Task<bool> CompleteWorkflowTaskAsync(string taskId, WorkflowDecision decision, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("CompleteWorkflowTaskAsync is deprecated. Human tasks should be completed via Camunda Tasklist API.");
        return false;
    }

    [Obsolete("Workflow steps are now tracked via Camunda History API. Use Camunda REST API directly for historical data.")]
    public async Task<List<WorkflowStep>> GetWorkflowStepsAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("GetWorkflowStepsAsync is deprecated. Use Camunda History API directly.");
        return new List<WorkflowStep>();
    }

    [Obsolete("Current workflow step should be queried via Camunda Tasklist API directly.")]
    public async Task<string?> GetCurrentWorkflowStepAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("GetCurrentWorkflowStepAsync is deprecated. Use Camunda Tasklist API directly.");
        return null;
    }

    [Obsolete("Task reassignment is now handled directly by Camunda Tasklist API. This method is deprecated.")]
    public async Task<bool> ReassignWorkflowTaskAsync(string taskId, string newAssignee, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("ReassignWorkflowTaskAsync is deprecated. Use Camunda Tasklist API directly for task management.");
        return false;
    }

    private async Task<CamundaProcessInstanceResponse?> StartCamundaProcessAsync(CamundaProcessInstance request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _camundaClient.PostAsJsonAsync($"process-definition/key/{request.ProcessDefinitionKey}/start", request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to start Camunda process. Status: {StatusCode}. Response: {ErrorResponse}", response.StatusCode, errorContent);
                return null;
            }
            return await response.Content.ReadFromJsonAsync<CamundaProcessInstanceResponse>(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting Camunda process for business key {BusinessKey}", request.BusinessKey);
            return null;
        }
    }

    private async Task<bool> CompleteCamundaTaskAsync(string taskId, CamundaTaskRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _camundaClient.PostAsJsonAsync($"task/{taskId}/complete", request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to complete Camunda task {TaskId}. Status: {StatusCode}. Response: {ErrorResponse}", taskId, response.StatusCode, errorContent);
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing Camunda task {TaskId}", taskId);
            return false;
        }
    }

    public Task<bool> AdvanceWorkflowAsync(Guid applicationId, string nextStep, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("AdvanceWorkflowAsync is deprecated. Workflow is now driven by Camunda.");
        return Task.FromResult(false);
    }
}
