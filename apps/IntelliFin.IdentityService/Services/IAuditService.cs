using IntelliFin.IdentityService.Models;

namespace IntelliFin.IdentityService.Services;

public interface IAuditService
{
    Task LogAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);
}
