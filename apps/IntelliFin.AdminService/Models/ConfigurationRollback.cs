using System;

namespace IntelliFin.AdminService.Models;

public class ConfigurationRollback
{
    public long Id { get; set; }
    public Guid RollbackId { get; set; }
    public Guid OriginalChangeRequestId { get; set; }
    public Guid NewChangeRequestId { get; set; }
    public string ConfigKey { get; set; } = string.Empty;
    public string RolledBackValue { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string RolledBackBy { get; set; } = string.Empty;
    public DateTime RolledBackAt { get; set; }
}
