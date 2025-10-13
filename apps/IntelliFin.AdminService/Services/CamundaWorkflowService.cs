using System;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using IntelliFin.AdminService.Models;
using IntelliFin.AdminService.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntelliFin.AdminService.Services;

public sealed class CamundaWorkflowService : ICamundaWorkflowService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<CamundaOptions> _optionsMonitor;
    private readonly ILogger<CamundaWorkflowService> _logger;

    public CamundaWorkflowService(HttpClient httpClient, IOptionsMonitor<CamundaOptions> optionsMonitor, ILogger<CamundaWorkflowService> logger)
    {
        _httpClient = httpClient;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public async Task<string?> StartElevationWorkflowAsync(ElevationRequest request, IReadOnlyCollection<string> roles, CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.CurrentValue;
        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            _logger.LogWarning("Camunda base URL not configured; returning synthetic process instance id for elevation {ElevationId}", request.ElevationId);
            return $"offline-{Guid.NewGuid():N}";
        }

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

        try
        {
            var response = await _httpClient.PostAsJsonAsync("workflows/access-elevation", payload, SerializerOptions, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadFromJsonAsync<CamundaStartResponse>(SerializerOptions, cancellationToken);
                if (!string.IsNullOrWhiteSpace(content?.ProcessInstanceId))
                {
                    return content.ProcessInstanceId;
                }
            }
            else
            {
                _logger.LogWarning("Camunda elevation workflow start returned status {StatusCode} for elevation {ElevationId}", response.StatusCode, request.ElevationId);
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Failed to invoke Camunda workflow for elevation {ElevationId}", request.ElevationId);
        }

        return $"fallback-{Guid.NewGuid():N}";
    }

    public async Task CompleteManagerApprovalAsync(string processInstanceId, bool approved, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(processInstanceId))
        {
            return;
        }

        var options = _optionsMonitor.CurrentValue;
        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            _logger.LogDebug("Camunda base URL not configured; skipping completion for process {ProcessInstanceId}", processInstanceId);
            return;
        }

        var payload = new { approved };

        try
        {
            var response = await _httpClient.PostAsJsonAsync($"workflows/access-elevation/{processInstanceId}/approval", payload, SerializerOptions, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Camunda manager approval completion returned status {StatusCode} for process {ProcessInstanceId}", response.StatusCode, processInstanceId);
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Failed to complete Camunda approval for process {ProcessInstanceId}", processInstanceId);
        }
    }

    public async Task<string?> StartConfigurationChangeWorkflowAsync(ConfigurationChange change, ConfigurationPolicy policy, string requestorName, CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.CurrentValue;
        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            _logger.LogDebug("Camunda base URL not configured; returning synthetic id for configuration change {ChangeId}", change.ChangeRequestId);
            return $"config-offline-{Guid.NewGuid():N}";
        }

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

        try
        {
            var response = await _httpClient.PostAsJsonAsync("workflows/config-change", payload, SerializerOptions, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadFromJsonAsync<CamundaStartResponse>(SerializerOptions, cancellationToken);
                if (!string.IsNullOrWhiteSpace(content?.ProcessInstanceId))
                {
                    return content.ProcessInstanceId;
                }
            }
            else
            {
                _logger.LogWarning("Camunda configuration workflow start returned {StatusCode} for change {ChangeId}", response.StatusCode, change.ChangeRequestId);
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Failed to invoke Camunda configuration workflow for change {ChangeId}", change.ChangeRequestId);
        }

        return $"config-fallback-{Guid.NewGuid():N}";
    }

    public async Task CompleteConfigurationChangeWorkflowAsync(string? processInstanceId, bool approved, string comments, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(processInstanceId))
        {
            return;
        }

        var options = _optionsMonitor.CurrentValue;
        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            _logger.LogDebug("Camunda base URL not configured; skipping completion for config process {ProcessId}", processInstanceId);
            return;
        }

        var payload = new { approved, comments };

        try
        {
            var response = await _httpClient.PostAsJsonAsync($"workflows/config-change/{processInstanceId}/approval", payload, SerializerOptions, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Camunda config approval completion returned {StatusCode} for process {ProcessId}", response.StatusCode, processInstanceId);
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Failed to complete Camunda configuration workflow for process {ProcessId}", processInstanceId);
        }
    }

    public async Task<string?> StartRecertificationCampaignAsync(string campaignId, CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.CurrentValue;
        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            _logger.LogDebug("Camunda base URL not configured; returning synthetic id for recertification campaign {CampaignId}", campaignId);
            return $"recert-offline-{Guid.NewGuid():N}";
        }

        var payload = new { campaignId };

        try
        {
            var response = await _httpClient.PostAsJsonAsync("workflows/recertification", payload, SerializerOptions, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadFromJsonAsync<CamundaStartResponse>(SerializerOptions, cancellationToken);
                if (!string.IsNullOrWhiteSpace(content?.ProcessInstanceId))
                {
                    return content.ProcessInstanceId;
                }
            }
            else
            {
                _logger.LogWarning("Camunda recertification workflow start returned {StatusCode} for campaign {CampaignId}", response.StatusCode, campaignId);
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Failed to start Camunda recertification workflow for campaign {CampaignId}", campaignId);
        }

        return $"recert-fallback-{Guid.NewGuid():N}";
    }

    public async Task CompleteRecertificationTaskAsync(string? camundaTaskId, Guid taskId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(camundaTaskId))
        {
            _logger.LogDebug("No Camunda task id provided for recertification task {TaskId}", taskId);
            return;
        }

        var options = _optionsMonitor.CurrentValue;
        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            _logger.LogDebug("Camunda base URL not configured; skipping completion for recertification task {TaskId}", taskId);
            return;
        }

        try
        {
            var response = await _httpClient.PostAsJsonAsync($"workflows/recertification/{camundaTaskId}/complete", new { taskId }, SerializerOptions, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Camunda recertification completion returned {StatusCode} for task {TaskId}", response.StatusCode, taskId);
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Failed to complete Camunda recertification task {TaskId}", taskId);
        }
    }

    private sealed record CamundaStartResponse(string? ProcessInstanceId);
}
