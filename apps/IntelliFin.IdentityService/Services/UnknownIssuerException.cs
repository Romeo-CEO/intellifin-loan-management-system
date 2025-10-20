namespace IntelliFin.IdentityService.Services;

public class UnknownIssuerException : Exception
{
    public UnknownIssuerException(string issuer)
        : base($"Issuer '{issuer}' is not trusted for introspection.")
    {
        Issuer = issuer;
    }

    public string Issuer { get; }
}
