using System.ComponentModel.DataAnnotations;

namespace IntelliFin.TreasuryService.Models;

public class ExpenseRequest
{
    public int Id { get; set; }

    [Required]
    public Guid RequestId { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(20)]
    public string BranchId { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string RequestedBy { get; set; } = string.Empty;

    [Required]
    public decimal Amount { get; set; }

    [Required]
    [MaxLength(3)]
    public string Currency { get; set; } = "MWK";

    [Required]
    [MaxLength(50)]
    public string Category { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected, Processed

    [Required]
    [MaxLength(20)]
    public string Urgency { get; set; } = "Normal"; // Low, Normal, High, Critical

    [MaxLength(1000)]
    public string? ReceiptPath { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<ExpenseApproval> Approvals { get; set; } = new List<ExpenseApproval>();
}

