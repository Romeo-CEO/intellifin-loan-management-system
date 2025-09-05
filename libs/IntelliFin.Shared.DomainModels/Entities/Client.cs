namespace IntelliFin.Shared.DomainModels.Entities;

public class Client
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string NationalId { get; set; } = string.Empty; // NRC
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<LoanApplication> LoanApplications { get; set; } = new List<LoanApplication>();
}

