using System;

namespace IntelliFin.AdminService.Models;

public class RoleHierarchyEntry
{
    public int Id { get; set; }
    public string ParentRole { get; set; } = string.Empty;
    public string ChildRole { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
