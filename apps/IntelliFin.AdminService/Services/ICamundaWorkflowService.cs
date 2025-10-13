using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IntelliFin.AdminService.Models;

namespace IntelliFin.AdminService.Services;

public interface ICamundaWorkflowService
{
    Task<string?> StartElevationWorkflowAsync(ElevationRequest request, IReadOnlyCollection<string> roles, CancellationToken cancellationToken);
    Task CompleteManagerApprovalAsync(string processInstanceId, bool approved, CancellationToken cancellationToken);
    Task<string?> StartConfigurationChangeWorkflowAsync(ConfigurationChange change, ConfigurationPolicy policy, string requestorName, CancellationToken cancellationToken);
    Task CompleteConfigurationChangeWorkflowAsync(string? processInstanceId, bool approved, string comments, CancellationToken cancellationToken);
    Task<string?> StartRecertificationCampaignAsync(string campaignId, CancellationToken cancellationToken);
    Task CompleteRecertificationTaskAsync(string? camundaTaskId, Guid taskId, CancellationToken cancellationToken);
    Task<string?> StartIncidentWorkflowAsync(OperationalIncident incident, CancellationToken cancellationToken);
    Task<string?> StartPostIncidentReviewAsync(OperationalIncident incident, DateTime dueAtUtc, CancellationToken cancellationToken);
}
