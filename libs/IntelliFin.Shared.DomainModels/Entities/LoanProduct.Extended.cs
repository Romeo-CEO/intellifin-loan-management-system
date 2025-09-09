namespace IntelliFin.Shared.DomainModels.Entities;

public class ApplicationField
{
    public Guid Id { get; set; }
    public Guid LoanProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public int Order { get; set; }
    public string DefaultValue { get; set; } = string.Empty;
    public string ValidationPattern { get; set; } = string.Empty;
    public string HelpText { get; set; } = string.Empty;
    public string OptionsJson { get; set; } = string.Empty;

    public LoanProduct? LoanProduct { get; set; }
}

public class ValidationRule
{
    public Guid Id { get; set; }
    public Guid LoanProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Expression { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;

    public LoanProduct? LoanProduct { get; set; }
}