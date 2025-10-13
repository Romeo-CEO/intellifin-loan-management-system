namespace IntelliFin.AdminService.Contracts.Responses;

public record ArgoCdApplicationDto(
    string Name,
    string Namespace,
    string Project,
    string SyncStatus,
    string HealthStatus,
    string Revision,
    DateTimeOffset? LastSyncedAt,
    string DestinationServer
);
