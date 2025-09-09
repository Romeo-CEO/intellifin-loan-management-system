namespace IntelliFin.Shared.DomainModels.Entities;

public class UserRole
{
    public string UserId { get; set; } = string.Empty;
    public string RoleId { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public string AssignedBy { get; set; } = string.Empty;
    public string? BranchId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? ExpiresAt { get; set; }
    public string? Reason { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();

    // Navigation properties
    public virtual Role Role { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}