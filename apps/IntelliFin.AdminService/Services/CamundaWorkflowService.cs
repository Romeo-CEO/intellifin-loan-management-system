using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using IntelliFin.AdminService.ExceptionHandling;
using IntelliFin.AdminService.Models;
using IntelliFin.AdminService.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntelliFin.AdminService.Services;

public sealed class CamundaWorkflowService : ICamundaWorkflowService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly Meter Meter = new("IntelliFin.AdminService.Camunda", "1.0.0");
    private static readonly Counter<long> FailureCounter = Meter.CreateCounter<long>("camunda.workflow.failures");

    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<CamundaOptions> _optionsMonitor;
    private readonly IOptionsMonitor<IncidentResponseOptions> _incidentOptions;
    private readonly ILogger<CamundaWorkflowService> _logger;

    public CamundaWorkflowService(
        HttpClient httpClient,
        IOptionsMonitor<CamundaOptions> optionsMonitor,
        IOptionsMonitor<IncidentResponseOptions> incidentOptions,
        ILogger<CamundaWorkflowService> logger)
    {
        _httpClient = httpClient;
        _optionsMonitor = optionsMonitor;
        _incidentOptions = incidentOptions;
        _logger = logger;
    }

    public async Task<string?> StartElevationWorkflowAsync(ElevationRequest request, IReadOnlyCollection<string> roles, CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.CurrentValue;
        if (ShouldFailOpen(options, "access_elevation", request.ElevationId.ToString()))
        {
            return null;
        }

        EnsureConfigured(options);

        var payload = new
        {
            elevationId = request.ElevationId,
            userId = request.UserId,
            userName = request.UserName,
            managerId = request.ManagerId,
            justification = request.Justification,
            requestedRoles = roles,
            requestedDuration = request.RequestedDuration
        };

        var response = await _httpClient.PostAsJsonAsync("workflows/access-elevation", payload, SerializerOptions, cancellationToken).ConfigureAwait(false);
        return await ReadProcessInstanceIdAsync(response, "workflows/access-elevation", "access_elevation", request.ElevationId.ToString(), cancellationToken).ConfigureAwait(false);
    }

    public async Task CompleteManagerApprovalAsync(string processInstanceId, bool approved, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(processInstanceId))
        {
            return;
        }

        var options = _optionsMonitor.CurrentValue;
        if (ShouldFailOpen(options, "access_elevation_approval", processInstanceId))
        {
            return;
        }

        EnsureConfigured(options);

        var payload = new { approved };
        var response = await _httpClient.PostAsJsonAsync($"workflows/access-elevation/{processInstanceId}/approval", payload, SerializerOptions, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, $"workflows/access-elevation/{processInstanceId}/approval", "access_elevation_approval", processInstanceId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> StartConfigurationChangeWorkflowAsync(ConfigurationChange change, ConfigurationPolicy policy, string requestorName, CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.CurrentValue;
        if (ShouldFailOpen(options, "config_change", change.ChangeRequestId.ToString()))
        {
            return null;
        }

        EnsureConfigured(options);

        var payload = new
        {
            changeId = change.ChangeRequestId,
            configKey = change.ConfigKey,
            oldValue = change.OldValue ?? string.Empty,
            newValue = change.NewValue,
            justification = change.Justification,
            requestedBy = requestorName,
            sensitivity = policy.Sensitivity,
            requiresApproval = policy.RequiresApproval
        };

        var response = await _httpClient.PostAsJsonAsync("workflows/config-change", payload, SerializerOptions, cancellationToken).ConfigureAwait(false);
        return await ReadProcessInstanceIdAsync(response, "workflows/config-change", "config_change", change.ChangeRequestId.ToString(), cancellationToken).ConfigureAwait(false);
    }

    public async Task CompleteConfigurationChangeWorkflowAsync(string? processInstanceId, bool approved, string comments, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(processInstanceId))
        {
            return;
        }

        var options = _optionsMonitor.CurrentValue;
        if (ShouldFailOpen(options, "config_change_approval", processInstanceId))
        {
            return;
        }

        EnsureConfigured(options);

        var payload = new { approved, comments };
        var response = await _httpClient.PostAsJsonAsync($"workflows/config-change/{processInstanceId}/approval", payload, SerializerOptions, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, $"workflows/config-change/{processInstanceId}/approval", "config_change_approval", processInstanceId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> StartRecertificationCampaignAsync(string campaignId, CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.CurrentValue;
        if (ShouldFailOpen(options, "recertification_campaign", campaignId))
        {
            return null;
        }

        EnsureConfigured(options);

        var payload = new { campaignId };
        var response = await _httpClient.PostAsJsonAsync("workflows/recertification", payload, SerializerOptions, cancellationToken).ConfigureAwait(false);
        return await ReadProcessInstanceIdAsync(response, "workflows/recertification", "recertification_campaign", campaignId, cancellationToken).ConfigureAwait(false);
    }

    public async Task CompleteRecertificationTaskAsync(string? camundaTaskId, Guid taskId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(camundaTaskId))
        {
            return;
        }

        var options = _optionsMonitor.CurrentValue;
        if (ShouldFailOpen(options, "recertification_task", camundaTaskId))
        {
            return;
        }

        EnsureConfigured(options);

        var response = await _httpClient.PostAsJsonAsync($"workflows/recertification/{camundaTaskId}/complete", new { taskId }, SerializerOptions, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, $"workflows/recertification/{camundaTaskId}/complete", "recertification_task", camundaTaskId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> StartIncidentWorkflowAsync(OperationalIncident incident, CancellationToken cancellationToken)
    {
        var camundaOptions = _optionsMonitor.CurrentValue;
        var incidentOptions = _incidentOptions.CurrentValue;

        if (string.IsNullOrWhiteSpace(incidentOptions.IncidentWorkflowProcessId))
        {
            _logger.LogWarning("Incident workflow process id is not configured; skipping Camunda incident workflow for {IncidentId}", incident.IncidentId);
            return null;
        }

        if (ShouldFailOpen(camundaOptions, "incident_workflow", incident.IncidentId.ToString()))
        {
            return null;
        }

        EnsureConfigured(camundaOptions);

        var payload = new
        {
            incidentId = incident.IncidentId,
            alertName = incident.AlertName,
            severity = incident.Severity,
            detectedAt = incident.DetectedAt,
            summary = incident.Summary,
            pagerDutyIncidentId = incident.PagerDutyIncidentId,
            slackThreadUrl = incident.SlackThreadUrl,
            playbookId = incident.Playbook?.PlaybookId,
            automationStatus = incident.AutomationStatus
        };

        var response = await _httpClient.PostAsJsonAsync($"workflows/{incidentOptions.IncidentWorkflowProcessId}", payload, SerializerOptions, cancellationToken).ConfigureAwait(false);
        return await ReadProcessInstanceIdAsync(response, $"workflows/{incidentOptions.IncidentWorkflowProcessId}", "incident_workflow", incident.IncidentId.ToString(), cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> StartPostIncidentReviewAsync(OperationalIncident incident, DateTime dueAtUtc, CancellationToken cancellationToken)
    {
        var camundaOptions = _optionsMonitor.CurrentValue;
        var incidentOptions = _incidentOptions.CurrentValue;

        if (string.IsNullOrWhiteSpace(incidentOptions.PostmortemWorkflowProcessId))
        {
            _logger.LogWarning("Postmortem workflow process id is not configured; skipping Camunda post-incident workflow for {IncidentId}", incident.IncidentId);
            return null;
        }

        if (ShouldFailOpen(camundaOptions, "incident_postmortem", incident.IncidentId.ToString()))
        {
            return null;
        }

        EnsureConfigured(camundaOptions);

        var payload = new
        {
            incidentId = incident.IncidentId,
            alertName = incident.AlertName,
            severity = incident.Severity,
            dueAt = dueAtUtc,
            summary = incident.Summary,
            owner = incident.CreatedBy
        };

        var response = await _httpClient.PostAsJsonAsync($"workflows/{incidentOptions.PostmortemWorkflowProcessId}", payload, SerializerOptions, cancellationToken).ConfigureAwait(false);
        return await ReadProcessInstanceIdAsync(response, $"workflows/{incidentOptions.PostmortemWorkflowProcessId}", "incident_postmortem", incident.IncidentId.ToString(), cancellationToken).ConfigureAwait(false);
    }

    private bool ShouldFailOpen(CamundaOptions options, string workflowType, string entityId)
    {
        if (options.FailOpen)
        {
            var correlationId = Activity.Current?.TraceId.ToString();
            _logger.LogWarning(
                "Camunda FailOpen enabled; skipping workflow {WorkflowType} for {EntityId} (CorrelationId: {CorrelationId})",
                workflowType,
                entityId,
                correlationId);
            return true;
        }

        return false;
    }

    private void EnsureConfigured(CamundaOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            throw new InvalidOperationException("Camunda BaseUrl configuration is required when fail-open is disabled.");
        }
    }

    private async Task<string> ReadProcessInstanceIdAsync(HttpResponseMessage response, string endpoint, string workflowType, string entityId, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            await ThrowCamundaExceptionAsync(response, endpoint, workflowType, entityId, cancellationToken).ConfigureAwait(false);
        }

        var content = await response.Content.ReadFromJsonAsync<CamundaStartResponse>(SerializerOptions, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(content?.ProcessInstanceId))
        {
            return content.ProcessInstanceId;
        }

        await ThrowCamundaExceptionAsync(response, endpoint, workflowType, entityId, cancellationToken, "Camunda response did not include a processInstanceId.").ConfigureAwait(false);
        return string.Empty; // Unreachable
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response, string endpoint, string workflowType, string entityId, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        await ThrowCamundaExceptionAsync(response, endpoint, workflowType, entityId, cancellationToken).ConfigureAwait(false);
    }

    private async Task ThrowCamundaExceptionAsync(HttpResponseMessage response, string endpoint, string workflowType, string entityId, CancellationToken cancellationToken, string? overrideMessage = null)
    {
        var correlationId = Activity.Current?.TraceId.ToString();
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var statusCode = response.StatusCode;

        _logger.LogWarning(
            "Camunda workflow failure. WorkflowType={WorkflowType} EntityId={EntityId} Status={StatusCode} CorrelationId={CorrelationId} Body={Body}",
            workflowType,
            entityId,
            (int)statusCode,
            correlationId,
            body);

        FailureCounter.Add(1, new KeyValuePair<string, object?>("workflow_type", workflowType), new KeyValuePair<string, object?>("status_code", (int)statusCode));

        throw new CamundaWorkflowException(statusCode, endpoint, workflowType, overrideMessage ?? body, correlationId);
    }

    private sealed record CamundaStartResponse(string? ProcessInstanceId);
}
