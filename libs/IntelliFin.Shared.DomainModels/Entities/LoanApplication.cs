namespace IntelliFin.Shared.DomainModels.Entities;

public class LoanApplication
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public decimal Amount { get; set; }
    public int TermMonths { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string Status { get; set; } = "Created";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Client? Client { get; set; }
}

