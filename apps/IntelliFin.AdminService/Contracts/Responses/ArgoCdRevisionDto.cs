namespace IntelliFin.AdminService.Contracts.Responses;

public record ArgoCdRevisionDto(
    int Id,
    string Revision,
    string Author,
    string Message,
    DateTimeOffset DeployedAt,
    string Status
);
