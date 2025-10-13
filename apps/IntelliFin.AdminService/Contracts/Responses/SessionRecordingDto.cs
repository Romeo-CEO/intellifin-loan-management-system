namespace IntelliFin.AdminService.Contracts.Responses;

public record SessionRecordingDto(
    Guid SessionId,
    string RecordingPath,
    string? DownloadUrl,
    DateTime StartTime,
    DateTime? EndTime,
    string BastionHost,
    string? TargetHost,
    string Username);
