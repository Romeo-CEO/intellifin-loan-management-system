using System;

namespace IntelliFin.AdminService.Models;

public class MfaConfiguration
{
    public int Id { get; set; }
    public string OperationName { get; set; } = string.Empty;
    public bool RequiresMfa { get; set; } = true;
    public int TimeoutMinutes { get; set; } = 15;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
