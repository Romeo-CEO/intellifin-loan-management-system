using System.ComponentModel.DataAnnotations;

namespace IntelliFin.TreasuryService.Models;

public class EndOfDayReport
{
    public int Id { get; set; }

    [Required]
    public Guid ReportId { get; set; } = Guid.NewGuid();

    [Required]
    public DateTime ReportDate { get; set; }

    [Required]
    [MaxLength(20)]
    public string BranchId { get; set; } = string.Empty;

    [Required]
    public decimal OpeningBalance { get; set; }

    [Required]
    public decimal ClosingBalance { get; set; }

    public decimal? TotalDisbursements { get; set; }

    public decimal? TotalCollections { get; set; }

    public decimal? TotalExpenses { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "InProgress"; // InProgress, Completed, Override

    public DateTime? GeneratedAt { get; set; }

    [MaxLength(100)]
    public string? GeneratedBy { get; set; }

    [MaxLength(100)]
    public string? CeoOverrideBy { get; set; }

    [MaxLength(1000)]
    public string? CeoOverrideReason { get; set; }

    public DateTime? CeoOverrideAt { get; set; }

    [MaxLength(1000)]
    public string? FilePath { get; set; }
}

