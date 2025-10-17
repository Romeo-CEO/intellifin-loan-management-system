namespace IntelliFin.IdentityService.Models;

/// <summary>
/// Request to create a new tenant
/// </summary>
public class TenantCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Settings { get; set; }
}

/// <summary>
/// Tenant data transfer object
/// </summary>
public class TenantDto
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Settings { get; set; }
}

/// <summary>
/// Request to assign a user to a tenant
/// </summary>
public class UserAssignmentRequest
{
    public string UserId { get; set; } = string.Empty;
    public string? Role { get; set; }
}

/// <summary>
/// Paged result wrapper
/// </summary>
/// <typeparam name="T">Type of items in the result</typeparam>
public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}
