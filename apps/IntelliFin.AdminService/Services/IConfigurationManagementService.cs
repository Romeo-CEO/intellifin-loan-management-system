using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IntelliFin.AdminService.Contracts.Requests;
using IntelliFin.AdminService.Contracts.Responses;

namespace IntelliFin.AdminService.Services;

public interface IConfigurationManagementService
{
    Task<IReadOnlyCollection<ConfigurationPolicyDto>> GetPoliciesAsync(string? category, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ConfigurationValueDto>> GetCurrentValuesAsync(string? category, CancellationToken cancellationToken);
    Task<ConfigChangeResponse> RequestChangeAsync(ConfigChangeRequest request, string requestorId, string requestorName, CancellationToken cancellationToken);
    Task<ConfigChangeStatusDto?> GetChangeRequestStatusAsync(Guid changeRequestId, CancellationToken cancellationToken);
    Task<PagedResult<ConfigChangeSummaryDto>> ListChangeRequestsAsync(string? status, int page, int pageSize, CancellationToken cancellationToken);
    Task ApproveChangeAsync(Guid changeRequestId, string approverId, string approverName, string? comments, CancellationToken cancellationToken);
    Task RejectChangeAsync(Guid changeRequestId, string reviewerId, string reviewerName, string reason, CancellationToken cancellationToken);
    Task<ConfigRollbackResponse> RollbackChangeAsync(ConfigRollbackRequest request, string adminId, string adminName, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ConfigChangeHistoryDto>> GetChangeHistoryAsync(string configKey, int limit, CancellationToken cancellationToken);
    Task UpdatePolicyAsync(int policyId, ConfigPolicyUpdateDto update, string adminId, CancellationToken cancellationToken);
}
