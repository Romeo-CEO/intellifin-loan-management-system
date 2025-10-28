using System.ComponentModel.DataAnnotations;

namespace IntelliFin.TreasuryService.Models;

public class BranchFloat
{
    public int Id { get; set; }

    [Required]
    [MaxLength(20)]
    public string BranchId { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string BranchName { get; set; } = string.Empty;

    [Required]
    public decimal CurrentBalance { get; set; }

    [Required]
    public decimal LowThreshold { get; set; }

    [Required]
    public decimal HighThreshold { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Active";

    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string? LastUpdatedBy { get; set; }

    // Navigation properties
    public ICollection<BranchFloatTransaction> Transactions { get; set; } = new List<BranchFloatTransaction>();
}

