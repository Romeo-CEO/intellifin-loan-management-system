namespace IntelliFin.AdminService.Contracts.Responses;

public record BastionSessionDto(
    Guid SessionId,
    string Username,
    string ClientIp,
    string BastionHost,
    string? TargetHost,
    DateTime StartTime,
    DateTime? EndTime,
    int? DurationSeconds,
    string Status,
    int CommandCount);
