using System.ComponentModel.DataAnnotations;

namespace IntelliFin.TreasuryService.Models;

public class ReconciliationBatch
{
    public int Id { get; set; }

    [Required]
    public Guid BatchId { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(20)]
    public string BatchType { get; set; } = string.Empty; // BankStatement, PMEC, Manual

    [Required]
    [MaxLength(500)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    public int TotalEntries { get; set; }

    public int? ProcessedEntries { get; set; }

    public int? MatchedEntries { get; set; }

    public int? UnmatchedEntries { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Processing"; // Processing, Completed, Failed

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    [MaxLength(1000)]
    public string? ErrorMessage { get; set; }

    // Navigation properties
    public ICollection<ReconciliationEntry> Entries { get; set; } = new List<ReconciliationEntry>();
}

