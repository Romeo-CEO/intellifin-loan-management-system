using System.Threading;
using System.Threading.Tasks;
using IntelliFin.AdminService.Models;

namespace IntelliFin.AdminService.Services;

public interface ICamundaWorkflowService
{
    Task<string?> StartElevationWorkflowAsync(ElevationRequest request, IReadOnlyCollection<string> roles, CancellationToken cancellationToken);
    Task CompleteManagerApprovalAsync(string processInstanceId, bool approved, CancellationToken cancellationToken);
}
