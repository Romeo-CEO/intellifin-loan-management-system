namespace IntelliFin.CreditAssessmentService.Services.Integration;

public interface IPmecClient
{
    Task<PmecEmployeeData?> VerifyEmployeeAsync(string nrc, CancellationToken cancellationToken = default);
    Task<PmecSalaryData?> GetSalaryDataAsync(string nrc, CancellationToken cancellationToken = default);
}

public class PmecEmployeeData
{
    public string Nrc { get; set; } = string.Empty;
    public bool IsVerified { get; set; }
    public string EmployerCode { get; set; } = string.Empty;
    public string EmployerName { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public DateTime EmploymentStartDate { get; set; }
    public int EmploymentMonths { get; set; }
}

public class PmecSalaryData
{
    public string Nrc { get; set; } = string.Empty;
    public decimal GrossSalary { get; set; }
    public decimal NetSalary { get; set; }
    public decimal TotalDeductions { get; set; }
    public List<PmecDeduction> ExistingDeductions { get; set; } = new();
}

public class PmecDeduction
{
    public string DeductionCode { get; set; } = string.Empty;
    public string DeductionName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime StartDate { get; set; }
}
