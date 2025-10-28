using System.ComponentModel.DataAnnotations;

namespace IntelliFin.TreasuryService.Models;

public class BankStatementEntry
{
    public int Id { get; set; }

    [Required]
    public int StatementId { get; set; }

    [Required]
    public DateTime TransactionDate { get; set; }

    [Required]
    [MaxLength(100)]
    public string Reference { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public decimal Amount { get; set; }

    public decimal? Balance { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public BankStatement? BankStatement { get; set; }
}

