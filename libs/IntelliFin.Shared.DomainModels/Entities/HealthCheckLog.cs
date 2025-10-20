namespace IntelliFin.Shared.DomainModels.Entities;

public class HealthCheckLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Component { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // Healthy/Degraded/Unhealthy
    public string? Details { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}