namespace IntelliFin.UserMigration.Models;

public sealed class MigrationResult
{
    public int SuccessCount { get; set; }
    public int SkippedCount { get; set; }
    public List<FailedMigration> FailedUsers { get; } = new();

    public bool HasFailures => FailedUsers.Count > 0;
}

public sealed class FailedMigration
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}
