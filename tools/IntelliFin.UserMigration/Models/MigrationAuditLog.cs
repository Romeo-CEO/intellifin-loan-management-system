namespace IntelliFin.UserMigration.Models;

public class MigrationAuditLog
{
    public int Id { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}
