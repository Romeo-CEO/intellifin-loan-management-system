namespace IntelliFin.AdminService.Services;

public interface IAuditService
{
    Task LogEventAsync(
        string actor,
        string action,
        string entityType,
        string? entityId,
        object? eventData,
        string? correlationId,
        CancellationToken cancellationToken);
}
