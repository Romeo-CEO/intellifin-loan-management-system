namespace IntelliFin.ApiGateway.Secrets;

public interface ISecretResolver
{
    string? Resolve(string key);

    string Require(string key, string? fallback = null);
}
