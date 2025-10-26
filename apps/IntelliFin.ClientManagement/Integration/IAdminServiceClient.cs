using IntelliFin.ClientManagement.Integration.DTOs;
using Refit;

namespace IntelliFin.ClientManagement.Integration;

/// <summary>
/// Refit interface for AdminService audit logging API
/// </summary>
public interface IAdminServiceClient
{
    /// <summary>
    /// Logs a single audit event to AdminService
    /// </summary>
    [Post("/api/audit/events")]
    Task<AuditEventResponse> LogAuditEventAsync([Body] AuditEventDto auditEvent);

    /// <summary>
    /// Logs a batch of audit events to AdminService
    /// </summary>
    [Post("/api/audit/events/batch")]
    Task<BatchAuditResponse> LogAuditEventsBatchAsync([Body] List<AuditEventDto> auditEvents);
}
