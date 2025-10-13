using System.Threading;
using System.Threading.Tasks;
using IntelliFin.AdminService.Contracts.Requests;
using IntelliFin.AdminService.Contracts.Responses;

namespace IntelliFin.AdminService.Services;

public interface IAccessElevationService
{
    Task<ElevationRequestResponse> RequestElevationAsync(string userId, string userName, ElevationRequestDto request, CancellationToken cancellationToken);
    Task<ElevationStatusDto?> GetElevationStatusAsync(Guid elevationId, CancellationToken cancellationToken);
    Task<PagedResult<ElevationSummaryDto>> ListElevationsAsync(string requesterId, string? filter, int page, int pageSize, CancellationToken cancellationToken);
    Task ApproveElevationAsync(Guid elevationId, string managerId, string managerName, int approvedDuration, CancellationToken cancellationToken);
    Task RejectElevationAsync(Guid elevationId, string managerId, string managerName, string reason, CancellationToken cancellationToken);
    Task RevokeElevationAsync(Guid elevationId, string adminId, string adminName, string reason, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ActiveSessionDto>> GetActiveSessionsAsync(CancellationToken cancellationToken);
    Task<int> ExpireElevationsAsync(CancellationToken cancellationToken);
}
