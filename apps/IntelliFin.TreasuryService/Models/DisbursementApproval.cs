using System.ComponentModel.DataAnnotations;

namespace IntelliFin.TreasuryService.Models;

public class DisbursementApproval
{
    public int Id { get; set; }

    [Required]
    public Guid DisbursementId { get; set; }

    [Required]
    [MaxLength(100)]
    public string ApproverId { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string ApproverName { get; set; } = string.Empty;

    [Required]
    public int ApprovalLevel { get; set; }

    [Required]
    [MaxLength(20)]
    public string Decision { get; set; } = string.Empty; // Approved, Rejected

    [MaxLength(1000)]
    public string? Comments { get; set; }

    public DateTime ApprovedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string? CorrelationId { get; set; }

    // Navigation properties
    public LoanDisbursement? LoanDisbursement { get; set; }
}
