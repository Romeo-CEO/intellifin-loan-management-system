using System.ComponentModel.DataAnnotations;

namespace IntelliFin.TreasuryService.Models;

public class LoanDisbursement
{
    public int Id { get; set; }

    [Required]
    public Guid DisbursementId { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(50)]
    public string LoanId { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string ClientId { get; set; } = string.Empty;

    [Required]
    public decimal Amount { get; set; }

    [Required]
    [MaxLength(3)]
    public string Currency { get; set; } = "MWK";

    [Required]
    [MaxLength(50)]
    public string BankAccountNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string BankCode { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? BankReference { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected, Processed, Failed

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string? RequestedBy { get; set; }

    public DateTime? ProcessedAt { get; set; }

    [MaxLength(100)]
    public string? ProcessedBy { get; set; }

    [MaxLength(100)]
    public string? CorrelationId { get; set; }

    // Navigation properties
    public ICollection<DisbursementApproval> Approvals { get; set; } = new List<DisbursementApproval>();
}

