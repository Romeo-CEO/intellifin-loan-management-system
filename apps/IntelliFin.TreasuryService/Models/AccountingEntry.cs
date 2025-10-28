using System.ComponentModel.DataAnnotations;

namespace IntelliFin.TreasuryService.Models;

public class AccountingEntry
{
    public int Id { get; set; }

    [Required]
    public Guid EntryId { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(20)]
    public string AccountCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string AccountName { get; set; } = string.Empty;

    public decimal? DebitAmount { get; set; }

    public decimal? CreditAmount { get; set; }

    [Required]
    public DateTime TransactionDate { get; set; }

    [Required]
    [MaxLength(100)]
    public string Reference { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string? SourceTransactionId { get; set; }

    [MaxLength(100)]
    public string? BatchId { get; set; }

    public DateTime PostedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string? PostedBy { get; set; }
}

