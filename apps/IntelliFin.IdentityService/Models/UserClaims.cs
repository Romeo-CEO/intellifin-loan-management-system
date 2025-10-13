using System.Security.Claims;

namespace IntelliFin.IdentityService.Models;

public class UserClaims
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string[] Roles { get; set; } = Array.Empty<string>();
    public string[] Permissions { get; set; } = Array.Empty<string>();
    public string? BranchId { get; set; }
    public string? BranchName { get; set; }
    public string? BranchRegion { get; set; }
    public string? TenantId { get; set; }
    public string? SessionId { get; set; }
    public string? DeviceId { get; set; }
    public DateTime AuthenticatedAt { get; set; }
    public string AuthenticationLevel { get; set; } = "basic";
    public string? IpAddress { get; set; }
    
    /// <summary>
    /// Rule-based authorization claims with values (e.g., "loan_approval_limit": "50000")
    /// </summary>
    public Dictionary<string, string> Rules { get; set; } = new();

    public List<Claim> ToClaims()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, UserId),
            new(ClaimTypes.Name, Username),
            new(ClaimTypes.Email, Email),
            new(ClaimTypes.GivenName, FirstName),
            new(ClaimTypes.Surname, LastName),
            new("session_id", SessionId ?? string.Empty),
            new("device_id", DeviceId ?? string.Empty),
            new("auth_time", AuthenticatedAt.ToString("O")),
            new("auth_level", AuthenticationLevel),
            new("ip_address", IpAddress ?? string.Empty)
        };

        if (!string.IsNullOrEmpty(BranchId))
        {
            claims.Add(new Claim("branch_id", BranchId));
        }

        if (!string.IsNullOrWhiteSpace(BranchName))
        {
            claims.Add(new Claim("branch_name", BranchName));
        }

        if (!string.IsNullOrWhiteSpace(BranchRegion))
        {
            claims.Add(new Claim("branch_region", BranchRegion));
        }

        if (!string.IsNullOrEmpty(TenantId))
        {
            claims.Add(new Claim("tenant_id", TenantId));
        }

        foreach (var role in Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        foreach (var permission in Permissions)
        {
            claims.Add(new Claim("permission", permission));
        }

        // Add rule-based claims for runtime evaluation
        foreach (var rule in Rules)
        {
            claims.Add(new Claim(rule.Key, rule.Value));
        }

        return claims;
    }
}