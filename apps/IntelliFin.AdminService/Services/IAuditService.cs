using IntelliFin.AdminService.Models;

namespace IntelliFin.AdminService.Services;

public interface IAuditService
{
    Task LogEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken);
    Task<int> LogEventsBatchAsync(IEnumerable<AuditEvent> auditEvents, CancellationToken cancellationToken);
    Task<AuditEventPage> GetAuditEventsAsync(AuditEventFilter filter, CancellationToken cancellationToken);
    Task<IReadOnlyList<AuditEvent>> GetAllAuditEventsAsync(AuditEventFilter filter, CancellationToken cancellationToken);
    Task FlushBufferAsync(CancellationToken cancellationToken);
    AuditBufferMetrics GetBufferMetrics();
    Task<ChainVerificationResult> VerifyChainIntegrityAsync(DateTime? startDate, DateTime? endDate, string initiatedBy, CancellationToken cancellationToken);
    Task<AuditIntegrityStatus> GetIntegrityStatusAsync(CancellationToken cancellationToken);
    Task<VerificationHistoryPage> GetVerificationHistoryAsync(int page, int pageSize, CancellationToken cancellationToken);
}
