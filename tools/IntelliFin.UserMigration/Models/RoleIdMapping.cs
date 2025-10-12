namespace IntelliFin.UserMigration.Models;

public class RoleIdMapping
{
    public int Id { get; set; }
    public string AspNetRoleId { get; set; } = string.Empty;
    public string KeycloakRoleId { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public DateTime MigrationDate { get; set; }
}
