using System.ComponentModel.DataAnnotations;

namespace IntelliFin.UserMigration.Options;

public sealed class DatabaseOptions
{
    [Required]
    public string IdentityConnectionString { get; set; } = string.Empty;

    [Required]
    public string AdminConnectionString { get; set; } = string.Empty;
}
