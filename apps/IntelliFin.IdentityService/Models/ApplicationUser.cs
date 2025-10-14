using Microsoft.AspNetCore.Identity;

namespace IntelliFin.IdentityService.Models;

/// <summary>
/// Extended identity user model that retains the existing platform fields.
/// This model will be enriched in future stories (e.g., Keycloak integration).
/// </summary>
public class ApplicationUser : IdentityUser
{
    public Guid? TenantId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? BranchId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
