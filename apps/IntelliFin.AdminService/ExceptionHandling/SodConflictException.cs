namespace IntelliFin.AdminService.ExceptionHandling;

public sealed class SodConflictException : Exception
{
    public SodConflictException(string message, IReadOnlyCollection<string> conflictingRoles, string severity)
        : base(message)
    {
        ConflictingRoles = conflictingRoles;
        Severity = severity;
    }

    public IReadOnlyCollection<string> ConflictingRoles { get; }

    public string Severity { get; }
}
