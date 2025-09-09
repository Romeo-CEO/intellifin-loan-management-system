namespace IntelliFin.IdentityService.Models;

public class AuditEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ActorId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? SessionId { get; set; }
    public Dictionary<string, object> Details { get; set; } = new();
    public string? Result { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string Source { get; set; } = "IntelliFin.IdentityService";
    public string Severity { get; set; } = "Information";
}