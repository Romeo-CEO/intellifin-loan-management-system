namespace IntelliFin.UserMigration.Models;

public class AspNetUserRole
{
    public string UserId { get; set; } = string.Empty;
    public AspNetUser User { get; set; } = null!;

    public string RoleId { get; set; } = string.Empty;
    public AspNetRole Role { get; set; } = null!;
}
