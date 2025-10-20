using Microsoft.AspNetCore.Identity;

namespace IntelliFin.IdentityService.Models;

/// <summary>
/// ASP.NET Identity user for the IdentityService with extended profile fields
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }

    // Branch context
    public string? BranchId { get; set; }
    public string? BranchName { get; set; }
    public string? BranchRegion { get; set; }

    // Arbitrary metadata for extensions
    public Dictionary<string, object> Metadata { get; set; } = new();
}
