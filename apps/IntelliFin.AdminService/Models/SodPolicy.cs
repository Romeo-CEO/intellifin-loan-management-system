using System;

namespace IntelliFin.AdminService.Models;

public class SodPolicy
{
    public int Id { get; set; }
    public string Role1 { get; set; } = string.Empty;
    public string Role2 { get; set; } = string.Empty;
    public string ConflictDescription { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
