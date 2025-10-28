using System.ComponentModel.DataAnnotations;

namespace IntelliFin.TreasuryService.Models;

public class TreasuryTransaction
{
    public int Id { get; set; }

    [Required]
    public Guid TransactionId { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(50)]
    public string TransactionType { get; set; } = string.Empty;

    [Required]
    public decimal Amount { get; set; }

    [Required]
    [MaxLength(3)]
    public string Currency { get; set; } = "MWK";

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Pending";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ProcessedAt { get; set; }

    [MaxLength(100)]
    public string? CorrelationId { get; set; }

    [MaxLength(100)]
    public string? BankReference { get; set; }

    [MaxLength(1000)]
    public string? ErrorMessage { get; set; }

    // Navigation properties
    public ICollection<DisbursementApproval> Approvals { get; set; } = new List<DisbursementApproval>();
}

