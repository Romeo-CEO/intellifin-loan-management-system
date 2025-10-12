namespace IntelliFin.UserMigration.Models;

public class UserIdMapping
{
    public int Id { get; set; }
    public string AspNetUserId { get; set; } = string.Empty;
    public string KeycloakUserId { get; set; } = string.Empty;
    public DateTime MigrationDate { get; set; }
    public string MigrationStatus { get; set; } = string.Empty;
    public string? Notes { get; set; }
}
