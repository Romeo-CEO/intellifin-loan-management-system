using System.Security.Cryptography;
using System.Text;

namespace IntelliFin.IdentityService.Services;

/// <summary>
/// Helper utilities for PKCE (Proof Key for Code Exchange) generation
/// </summary>
public static class PkceHelper
{
    private const string CodeVerifierChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";

    /// <summary>
    /// Generate a cryptographically secure state parameter
    /// </summary>
    public static string GenerateState()
    {
        return Guid.NewGuid().ToString("N"); // 32 character hex string
    }

    /// <summary>
    /// Generate a cryptographically secure nonce parameter
    /// </summary>
    public static string GenerateNonce()
    {
        return Guid.NewGuid().ToString("N"); // 32 character hex string
    }

    /// <summary>
    /// Generate a PKCE code verifier (43-128 characters from allowed charset)
    /// </summary>
    public static string GenerateCodeVerifier()
    {
        const int length = 64; // Standard length
        var bytes = new byte[length];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);

        var result = new char[length];
        for (int i = 0; i < length; i++)
        {
            result[i] = CodeVerifierChars[bytes[i] % CodeVerifierChars.Length];
        }

        return new string(result);
    }

    /// <summary>
    /// Compute PKCE code challenge from code verifier using S256 method
    /// code_challenge = BASE64URL(SHA256(ASCII(code_verifier)))
    /// </summary>
    public static string ComputeCodeChallenge(string codeVerifier)
    {
        if (string.IsNullOrEmpty(codeVerifier))
        {
            throw new ArgumentNullException(nameof(codeVerifier));
        }

        using var sha256 = SHA256.Create();
        var challengeBytes = sha256.ComputeHash(Encoding.ASCII.GetBytes(codeVerifier));
        return Base64UrlEncode(challengeBytes);
    }

    /// <summary>
    /// Compute a hash of the user agent string for binding state to client
    /// </summary>
    public static string ComputeUserAgentHash(string userAgent)
    {
        if (string.IsNullOrEmpty(userAgent))
        {
            return string.Empty;
        }

        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(userAgent));
        return Convert.ToBase64String(hashBytes);
    }

    /// <summary>
    /// Base64 URL encoding without padding (RFC 7636)
    /// </summary>
    private static string Base64UrlEncode(byte[] input)
    {
        var base64 = Convert.ToBase64String(input);
        
        // Convert to URL-safe base64
        base64 = base64.Replace('+', '-');
        base64 = base64.Replace('/', '_');
        base64 = base64.TrimEnd('='); // Remove padding
        
        return base64;
    }
}
