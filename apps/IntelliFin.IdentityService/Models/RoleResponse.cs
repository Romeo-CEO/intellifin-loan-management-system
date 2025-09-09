using IntelliFin.Shared.DomainModels.Entities;
using IntelliFin.Shared.DomainModels.Enums;

namespace IntelliFin.IdentityService.Models;

public class RoleResponse
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public RoleType Type { get; set; }
    public bool IsActive { get; set; }
    public bool IsSystemRole { get; set; }
    public string? ParentRoleId { get; set; }
    public string? ParentRoleName { get; set; }
    public int Level { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string? UpdatedBy { get; set; }
    public PermissionResponse[] Permissions { get; set; } = Array.Empty<PermissionResponse>();
    public int UserCount { get; set; }
}

public class PermissionResponse
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public PermissionType Type { get; set; }
    public bool IsActive { get; set; }
    public bool IsSystemPermission { get; set; }
}