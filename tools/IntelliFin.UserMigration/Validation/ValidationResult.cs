namespace IntelliFin.UserMigration.Validation;

public sealed class ValidationResult
{
    public bool UserCountMatches { get; set; }
    public bool RoleCountMatches { get; set; }
    public bool AssignmentCountMatches { get; set; }
    public List<string> SampleErrors { get; } = new();

    public bool IsSuccessful => UserCountMatches && RoleCountMatches && AssignmentCountMatches && SampleErrors.Count == 0;
}
