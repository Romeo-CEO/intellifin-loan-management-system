namespace IntelliFin.CreditAssessmentService.Services.Models;

/// <summary>
/// Employment profile returned by PMEC integration.
/// </summary>
public sealed class PmecEmploymentProfile
{
    public bool IsEmployed { get; init; }
    public string Employer { get; init; } = string.Empty;
    public string PayrollNumber { get; init; } = string.Empty;
    public decimal GrossSalary { get; init; }
    public decimal NetSalary { get; init; }
    public DateTime LastPayrollDate { get; init; }
    public IReadOnlyCollection<string> PayrollFlags { get; init; } = Array.Empty<string>();
}
