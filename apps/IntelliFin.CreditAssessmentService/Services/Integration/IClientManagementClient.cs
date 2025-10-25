namespace IntelliFin.CreditAssessmentService.Services.Integration;

public interface IClientManagementClient
{
    Task<ClientKycData?> GetKycDataAsync(Guid clientId, CancellationToken cancellationToken = default);
    Task<ClientEmploymentData?> GetEmploymentDataAsync(Guid clientId, CancellationToken cancellationToken = default);
}

public class ClientKycData
{
    public Guid ClientId { get; set; }
    public bool IsVerified { get; set; }
    public DateTime? VerificationDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string VerificationStatus { get; set; } = string.Empty;
}

public class ClientEmploymentData
{
    public Guid ClientId { get; set; }
    public string EmployerName { get; set; } = string.Empty;
    public decimal MonthlyIncome { get; set; }
    public int EmploymentMonths { get; set; }
    public string EmploymentType { get; set; } = string.Empty;
}
