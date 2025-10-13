using System.Threading;
using System.Threading.Tasks;
using IntelliFin.AdminService.Models;

namespace IntelliFin.AdminService.Services;

public interface IElevationNotificationService
{
    Task NotifyManagerPendingAsync(ElevationRequest request, IReadOnlyCollection<string> roles, CancellationToken cancellationToken);
    Task NotifyRequesterApprovedAsync(ElevationRequest request, IReadOnlyCollection<string> roles, CancellationToken cancellationToken);
    Task NotifyRequesterRejectedAsync(ElevationRequest request, string reason, CancellationToken cancellationToken);
    Task NotifyRequesterRevokedAsync(ElevationRequest request, string reason, CancellationToken cancellationToken);
    Task NotifyRequesterExpiredAsync(ElevationRequest request, CancellationToken cancellationToken);
}
