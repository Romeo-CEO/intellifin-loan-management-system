namespace IntelliFin.Shared.DomainModels.Entities;

public class User
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public string? BranchId { get; set; }
    public string? BranchName
    {
        get => GetMetadataValue(BranchNameMetadataKey);
        set => SetMetadataValue(BranchNameMetadataKey, value);
    }

    public string? BranchRegion
    {
        get => GetMetadataValue(BranchRegionMetadataKey);
        set => SetMetadataValue(BranchRegionMetadataKey, value);
    }
    public bool EmailConfirmed { get; set; } = false;
    public bool PhoneNumberConfirmed { get; set; } = false;
    public bool TwoFactorEnabled { get; set; } = false;
    public bool LockoutEnabled { get; set; } = true;
    public DateTimeOffset? LockoutEnd { get; set; }
    public int AccessFailedCount { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string? UpdatedBy { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();

    // Navigation properties
    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public virtual ICollection<TenantUser> TenantUsers { get; set; } = new List<TenantUser>();
    // Note: Branch navigation property will be added when Branch entity is created

    // Computed properties
    public string FullName => $"{FirstName} {LastName}".Trim();
    public bool IsLockedOut => LockoutEnd.HasValue && LockoutEnd > DateTimeOffset.UtcNow;
    public bool CanLogin => IsActive && !IsLockedOut;

    private const string BranchNameMetadataKey = "branchName";
    private const string BranchRegionMetadataKey = "branchRegion";

    private string? GetMetadataValue(string key)
    {
        if (Metadata is not null && Metadata.TryGetValue(key, out var value) && value is not null)
        {
            return value.ToString();
        }

        return null;
    }

    private void SetMetadataValue(string key, string? value)
    {
        Metadata ??= new Dictionary<string, object>();

        if (string.IsNullOrWhiteSpace(value))
        {
            Metadata.Remove(key);
            return;
        }

        Metadata[key] = value;
    }
}
