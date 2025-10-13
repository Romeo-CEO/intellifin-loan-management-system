using System.Threading.Tasks;
using IntelliFin.AdminService.Models;
using Microsoft.Extensions.Logging;

namespace IntelliFin.AdminService.Services;

public sealed class ElevationNotificationService(ILogger<ElevationNotificationService> logger) : IElevationNotificationService
{
    private readonly ILogger<ElevationNotificationService> _logger = logger;

    public Task NotifyManagerPendingAsync(ElevationRequest request, IReadOnlyCollection<string> roles, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Elevation request {ElevationId} pending manager approval. ManagerId={ManagerId} Roles={Roles}", request.ElevationId, request.ManagerId, string.Join(',', roles));
        return Task.CompletedTask;
    }

    public Task NotifyRequesterApprovedAsync(ElevationRequest request, IReadOnlyCollection<string> roles, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Elevation request {ElevationId} approved for user {UserId}. Roles={Roles} Expiry={ExpiresAt}", request.ElevationId, request.UserId, string.Join(',', roles), request.ExpiresAt);
        return Task.CompletedTask;
    }

    public Task NotifyRequesterRejectedAsync(ElevationRequest request, string reason, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Elevation request {ElevationId} rejected for user {UserId}. Reason={Reason}", request.ElevationId, request.UserId, reason);
        return Task.CompletedTask;
    }

    public Task NotifyRequesterRevokedAsync(ElevationRequest request, string reason, CancellationToken cancellationToken)
    {
        _logger.LogWarning("Elevation request {ElevationId} revoked for user {UserId}. Reason={Reason}", request.ElevationId, request.UserId, reason);
        return Task.CompletedTask;
    }

    public Task NotifyRequesterExpiredAsync(ElevationRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Elevation request {ElevationId} expired for user {UserId} at {ExpiredAt}", request.ElevationId, request.UserId, request.ExpiredAt);
        return Task.CompletedTask;
    }
}
