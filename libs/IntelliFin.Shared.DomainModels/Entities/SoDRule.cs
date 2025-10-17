using IntelliFin.Shared.DomainModels.Enums;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace IntelliFin.Shared.DomainModels.Entities;

public class SoDRule
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public SoDEnforcementLevel Enforcement { get; set; } = SoDEnforcementLevel.Warning;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public string CreatedBy { get; set; } = "system";
    public string? UpdatedBy { get; set; }

    public string ConflictingPermissionsJson { get; set; } = "[]";

    [NotMapped]
    public IReadOnlyCollection<string> ConflictingPermissions
    {
        get
        {
            try
            {
                return JsonSerializer.Deserialize<IReadOnlyCollection<string>>(ConflictingPermissionsJson, SerializerOptions)
                    ?? Array.Empty<string>();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }
        set
        {
            ConflictingPermissionsJson = JsonSerializer.Serialize(value ?? Array.Empty<string>(), SerializerOptions);
        }
    }
}
