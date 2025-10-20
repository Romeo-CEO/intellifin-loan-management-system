namespace IntelliFin.Contracts.Responses;

public class PermissionChangeEntry
{
    public string PermissionId { get; set; } = string.Empty;
    public string ChangeType { get; set; } = string.Empty; // e.g. "Added", "Removed"
    public string ChangedBy { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
}
