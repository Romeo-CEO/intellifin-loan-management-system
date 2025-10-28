namespace IntelliFin.Shared.DomainModels.Entities;

/// <summary>
/// Maintains thread-safe sequential loan number generation per branch and year.
/// Composite primary key (BranchCode, Year) ensures unique sequences per branch/year.
/// </summary>
public class LoanNumberSequence
{
    /// <summary>Branch code identifier (e.g., "LUS" for Lusaka, "CHD" for Chadiza)</summary>
    public string BranchCode { get; set; } = string.Empty;
    
    /// <summary>Calendar year for sequence (e.g., 2025)</summary>
    public int Year { get; set; }
    
    /// <summary>Next available sequence number (atomically incremented)</summary>
    public int NextSequence { get; set; } = 1;
    
    /// <summary>Last update timestamp for audit purposes</summary>
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
}
