using System.ComponentModel.DataAnnotations;

namespace IntelliFin.TreasuryService.Models;

public class ReconciliationEntry
{
    public int Id { get; set; }

    [Required]
    public int BatchId { get; set; }

    [Required]
    [MaxLength(20)]
    public string EntryType { get; set; } = string.Empty; // BankEntry, SystemEntry

    [Required]
    public decimal Amount { get; set; }

    [Required]
    [MaxLength(100)]
    public string Reference { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public DateTime TransactionDate { get; set; }

    [Required]
    [MaxLength(20)]
    public string MatchStatus { get; set; } = "Unmatched"; // Unmatched, Matched, ManualReview

    public Guid? MatchedTransactionId { get; set; }

    public decimal? MatchConfidence { get; set; }

    [MaxLength(50)]
    public string? MatchMethod { get; set; } // Exact, Fuzzy, Manual

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ReconciliationBatch? Batch { get; set; }
}

