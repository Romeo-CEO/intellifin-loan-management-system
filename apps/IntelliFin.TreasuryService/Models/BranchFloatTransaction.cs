using System.ComponentModel.DataAnnotations;

namespace IntelliFin.TreasuryService.Models;

public class BranchFloatTransaction
{
    public int Id { get; set; }

    [Required]
    public Guid TransactionId { get; set; } = Guid.NewGuid();

    public int? BranchFloatId { get; set; }

    [Required]
    [MaxLength(20)]
    public string BranchId { get; set; } = string.Empty;

    [Required]
    public decimal Amount { get; set; }

    [Required]
    [MaxLength(20)]
    public string TransactionType { get; set; } = string.Empty; // Credit, Debit, TopUp, Disbursement

    [Required]
    public decimal BalanceAfter { get; set; }

    [MaxLength(100)]
    public string? Reference { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string? CreatedBy { get; set; }

    // Navigation properties
    public BranchFloat? BranchFloat { get; set; }
}

