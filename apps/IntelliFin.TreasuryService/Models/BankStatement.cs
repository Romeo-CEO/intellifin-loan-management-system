using System.ComponentModel.DataAnnotations;

namespace IntelliFin.TreasuryService.Models;

public class BankStatement
{
    public int Id { get; set; }

    [Required]
    public Guid StatementId { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(20)]
    public string BankCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string BankName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string AccountNumber { get; set; } = string.Empty;

    [Required]
    public DateTime StatementDate { get; set; }

    [Required]
    public decimal OpeningBalance { get; set; }

    [Required]
    public decimal ClosingBalance { get; set; }

    public decimal? TotalCredits { get; set; }

    public decimal? TotalDebits { get; set; }

    [MaxLength(1000)]
    public string? FilePath { get; set; }

    [MaxLength(1000)]
    public string? MinioObjectKey { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Processing"; // Processing, Processed, Failed

    public DateTime? ProcessedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<BankStatementEntry> Entries { get; set; } = new List<BankStatementEntry>();
}

