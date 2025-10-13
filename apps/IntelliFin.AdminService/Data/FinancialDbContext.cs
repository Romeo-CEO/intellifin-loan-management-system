using Microsoft.EntityFrameworkCore;

namespace IntelliFin.AdminService.Data;

public class FinancialDbContext(DbContextOptions<FinancialDbContext> options) : DbContext(options)
{
    public DbSet<LegacyAuditLog> AuditLogs => Set<LegacyAuditLog>();
}

public class LegacyAuditLog
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? CorrelationId { get; set; }
    public string? Details { get; set; }
}
