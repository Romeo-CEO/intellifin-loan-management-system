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

    /// <summary>
    /// Starts a new loan origination workflow instance in Camunda with all required variables.
    /// Includes dual control exclusion variables (createdBy, assessedBy) for segregation of duties.
    /// </summary>
    public async Task<string> StartLoanOriginationWorkflowAsync(
        Guid applicationId,
        Guid clientId,
        decimal loanAmount,
        string riskGrade,
        string productCode,
        int termMonths,
        string createdBy,
        string loanNumber,
        string? assessedBy = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Starting loan origination workflow for application {ApplicationId}, client {ClientId}, amount {LoanAmount}, risk grade {RiskGrade}",
                applicationId, clientId, loanAmount, riskGrade);

            // Create process variables object with all required fields
            // Including dual control exclusion variables (Story 1.7)
            var variables = new
            {
                applicationId = applicationId.ToString(),
                clientId = clientId.ToString(),
                loanAmount,
                riskGrade,
                productCode,
                termMonths,
                createdBy,
                assessedBy = assessedBy ?? "", // Set to empty string if null
                loanNumber
            };

            // Start workflow instance
            var processInstance = await _zeebeClient.NewCreateProcessInstanceCommand()
                .BpmnProcessId("loanOriginationProcess")
                .LatestVersion()
                .Variables(JsonSerializer.Serialize(variables))
                .Send(cancellationToken);

            var workflowInstanceKey = processInstance.ProcessInstanceKey.ToString();

            _logger.LogInformation(
                "Successfully started loan origination workflow. ApplicationId: {ApplicationId}, WorkflowInstanceKey: {WorkflowInstanceKey}, ProcessDefinitionKey: {ProcessDefinitionKey}",
                applicationId, workflowInstanceKey, processInstance.ProcessDefinitionKey);

            return workflowInstanceKey;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error starting loan origination workflow for application {ApplicationId}. Client: {ClientId}, Amount: {LoanAmount}",
                applicationId, clientId, loanAmount);
            throw;
        }
    }

    [Obsolete("Use StartLoanOriginationWorkflowAsync instead. This method has limited variable support.")]
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
    
    /// <summary>
    /// Updates workflow variables for dual control scenarios.
    /// Sets the firstApproverId variable to exclude the first approver from the second approval step.
    /// </summary>
    /// <param name="workflowInstanceKey">The workflow instance key/ID.</param>
    /// <param name="firstApproverId">The user ID of the first approver.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SetFirstApproverIdAsync(
        string workflowInstanceKey, 
        string firstApproverId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Setting firstApproverId variable for workflow {WorkflowInstanceKey}: {FirstApproverId}",
                workflowInstanceKey, firstApproverId);
            
            var variables = new { firstApproverId };
            
            await _zeebeClient.NewSetVariablesCommand(long.Parse(workflowInstanceKey))
                .Variables(JsonSerializer.Serialize(variables))
                .Send(cancellationToken);
            
            _logger.LogInformation(
                "Successfully set firstApproverId for workflow {WorkflowInstanceKey}",
                workflowInstanceKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error setting firstApproverId for workflow {WorkflowInstanceKey}",
                workflowInstanceKey);
            throw;
        }
    }
}
