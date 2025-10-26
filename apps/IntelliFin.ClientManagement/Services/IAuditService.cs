namespace IntelliFin.ClientManagement.Services;

/// <summary>
/// Service interface for audit logging
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Logs an audit event asynchronously (fire-and-forget with batching)
    /// </summary>
    Task LogAuditEventAsync(
        string action,
        string entityType,
        string entityId,
        string actor,
        object? eventData = null);

    /// <summary>
    /// Flushes any pending audit events immediately
    /// </summary>
    Task FlushAsync();
}
