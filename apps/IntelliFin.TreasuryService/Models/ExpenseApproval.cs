using System.ComponentModel.DataAnnotations;

namespace IntelliFin.TreasuryService.Models;

public class ExpenseApproval
{
    public int Id { get; set; }

    [Required]
    public int RequestId { get; set; }

    [Required]
    [MaxLength(100)]
    public string ApprovedBy { get; set; } = string.Empty;

    [Required]
    public int ApprovalLevel { get; set; }

    [Required]
    [MaxLength(20)]
    public string Decision { get; set; } = string.Empty; // Approved, Rejected

    [MaxLength(1000)]
    public string? Comments { get; set; }

    public DateTime ApprovedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ExpenseRequest? ExpenseRequest { get; set; }
}

