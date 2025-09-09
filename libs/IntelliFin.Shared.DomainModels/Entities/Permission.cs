using IntelliFin.Shared.DomainModels.Enums;

namespace IntelliFin.Shared.DomainModels.Entities;

public class Permission
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public PermissionType Type { get; set; } = PermissionType.Resource;
    public bool IsActive { get; set; } = true;
    public bool IsSystemPermission { get; set; } = false;
    public string? ParentPermissionId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string? UpdatedBy { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();

    // Navigation properties
    public virtual Permission? ParentPermission { get; set; }
    public virtual ICollection<Permission> ChildPermissions { get; set; } = new List<Permission>();
    public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}