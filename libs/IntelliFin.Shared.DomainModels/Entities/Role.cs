using IntelliFin.Shared.DomainModels.Enums;

namespace IntelliFin.Shared.DomainModels.Entities;

public class Role
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public RoleType Type { get; set; } = RoleType.Standard;
    public bool IsActive { get; set; } = true;
    public bool IsSystemRole { get; set; } = false;
    public string? ParentRoleId { get; set; }
    public int Level { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string? UpdatedBy { get; set; }

    // Navigation properties
    public virtual Role? ParentRole { get; set; }
    public virtual ICollection<Role> ChildRoles { get; set; } = new List<Role>();
    public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}