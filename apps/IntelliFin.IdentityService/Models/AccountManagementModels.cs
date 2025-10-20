namespace IntelliFin.IdentityService.Models;

/// <summary>
/// User profile data transfer object
/// </summary>
public class UserProfileDto
{
    public string Id { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? Email { get; set; }
    public bool EmailVerified { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? BranchId { get; set; }
    public string? BranchName { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Request to update user profile
/// </summary>
public class UpdateProfileRequest
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
}

/// <summary>
/// Result of profile update operation
/// </summary>
public class UpdateProfileResult
{
    public bool Success { get; set; }
    public UserProfileDto? Profile { get; set; }
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Request to change password
/// </summary>
public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

/// <summary>
/// Result of password change operation
/// </summary>
public class ChangePasswordResult
{
    public bool Success { get; set; }
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Session information DTO
/// </summary>
public class SessionDto
{
    public string? SessionId { get; set; }
    public string? IpAddress { get; set; }
    public DateTime Start { get; set; }
    public DateTime LastAccess { get; set; }
    public string? UserAgent { get; set; }
    public bool IsCurrent { get; set; }
}
