using IntelliFin.Shared.DomainModels.Enums;
using System.ComponentModel.DataAnnotations;

namespace IntelliFin.IdentityService.Models;

public class RoleRequest
{
    [Required(ErrorMessage = "Role name is required")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Role name must be between 2 and 100 characters")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Description must not exceed 500 characters")]
    public string Description { get; set; } = string.Empty;

    public RoleType Type { get; set; } = RoleType.Standard;

    public string? ParentRoleId { get; set; }

    public string[] PermissionIds { get; set; } = Array.Empty<string>();

    public bool IsActive { get; set; } = true;
}