namespace IntelliFin.AdminService.Models;

public sealed class AuditArchiveResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public int EventCount { get; init; }
    public string? FileName { get; init; }
    public string? ObjectKey { get; init; }
    public long FileSize { get; init; }
}
