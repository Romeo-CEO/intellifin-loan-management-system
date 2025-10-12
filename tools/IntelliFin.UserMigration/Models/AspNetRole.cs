namespace IntelliFin.UserMigration.Models;

public class AspNetRole
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public string ConcurrencyStamp { get; set; } = Guid.NewGuid().ToString();
    public string? Description { get; set; }

    public ICollection<AspNetUserRole> UserRoles { get; set; } = new List<AspNetUserRole>();
}
