using IntelliFin.IdentityService.Services;

namespace IntelliFin.IdentityService.Tests.Services;

public class PkceHelperTests
{
    [Fact]
    public void GenerateState_ReturnsValidState()
    {
        // Act
        var state = PkceHelper.GenerateState();

        // Assert
        Assert.NotNull(state);
        Assert.Equal(32, state.Length); // 32 character hex string
        Assert.Matches("^[a-f0-9]+$", state);
    }

    [Fact]
    public void GenerateState_ReturnsDifferentValues()
    {
        // Act
        var state1 = PkceHelper.GenerateState();
        var state2 = PkceHelper.GenerateState();

        // Assert
        Assert.NotEqual(state1, state2);
    }

    [Fact]
    public void GenerateNonce_ReturnsValidNonce()
    {
        // Act
        var nonce = PkceHelper.GenerateNonce();

        // Assert
        Assert.NotNull(nonce);
        Assert.Equal(32, nonce.Length);
        Assert.Matches("^[a-f0-9]+$", nonce);
    }

    [Fact]
    public void GenerateNonce_ReturnsDifferentValues()
    {
        // Act
        var nonce1 = PkceHelper.GenerateNonce();
        var nonce2 = PkceHelper.GenerateNonce();

        // Assert
        Assert.NotEqual(nonce1, nonce2);
    }

    [Fact]
    public void GenerateCodeVerifier_ReturnsValidVerifier()
    {
        // Act
        var verifier = PkceHelper.GenerateCodeVerifier();

        // Assert
        Assert.NotNull(verifier);
        Assert.Equal(64, verifier.Length);
        
        // Verify only allowed characters (A-Z, a-z, 0-9, -, ., _, ~)
        Assert.Matches("^[A-Za-z0-9\\-._~]+$", verifier);
    }

    [Fact]
    public void GenerateCodeVerifier_ReturnsDifferentValues()
    {
        // Act
        var verifier1 = PkceHelper.GenerateCodeVerifier();
        var verifier2 = PkceHelper.GenerateCodeVerifier();

        // Assert
        Assert.NotEqual(verifier1, verifier2);
    }

    [Fact]
    public void ComputeCodeChallenge_ReturnsValidChallenge()
    {
        // Arrange
        var verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";

        // Act
        var challenge = PkceHelper.ComputeCodeChallenge(verifier);

        // Assert
        Assert.NotNull(challenge);
        Assert.NotEmpty(challenge);
        
        // Base64URL encoded SHA256 should be 43 characters (no padding)
        Assert.Equal(43, challenge.Length);
        
        // Verify URL-safe characters only (A-Z, a-z, 0-9, -, _)
        Assert.Matches("^[A-Za-z0-9\\-_]+$", challenge);
        
        // Verify no padding
        Assert.DoesNotContain("=", challenge);
    }

    [Fact]
    public void ComputeCodeChallenge_SameVerifier_ReturnsSameChallenge()
    {
        // Arrange
        var verifier = "test-verifier-123";

        // Act
        var challenge1 = PkceHelper.ComputeCodeChallenge(verifier);
        var challenge2 = PkceHelper.ComputeCodeChallenge(verifier);

        // Assert
        Assert.Equal(challenge1, challenge2);
    }

    [Fact]
    public void ComputeCodeChallenge_DifferentVerifiers_ReturnsDifferentChallenges()
    {
        // Arrange
        var verifier1 = "verifier-1";
        var verifier2 = "verifier-2";

        // Act
        var challenge1 = PkceHelper.ComputeCodeChallenge(verifier1);
        var challenge2 = PkceHelper.ComputeCodeChallenge(verifier2);

        // Assert
        Assert.NotEqual(challenge1, challenge2);
    }

    [Fact]
    public void ComputeCodeChallenge_NullVerifier_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => PkceHelper.ComputeCodeChallenge(null!));
    }

    [Fact]
    public void ComputeCodeChallenge_EmptyVerifier_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => PkceHelper.ComputeCodeChallenge(string.Empty));
    }

    [Fact]
    public void ComputeUserAgentHash_ReturnsValidHash()
    {
        // Arrange
        var userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36";

        // Act
        var hash = PkceHelper.ComputeUserAgentHash(userAgent);

        // Assert
        Assert.NotNull(hash);
        Assert.NotEmpty(hash);
        
        // SHA256 base64 encoded should be 44 characters
        Assert.Equal(44, hash.Length);
    }

    [Fact]
    public void ComputeUserAgentHash_SameUA_ReturnsSameHash()
    {
        // Arrange
        var userAgent = "Test Browser 1.0";

        // Act
        var hash1 = PkceHelper.ComputeUserAgentHash(userAgent);
        var hash2 = PkceHelper.ComputeUserAgentHash(userAgent);

        // Assert
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeUserAgentHash_DifferentUA_ReturnsDifferentHash()
    {
        // Arrange
        var userAgent1 = "Browser A";
        var userAgent2 = "Browser B";

        // Act
        var hash1 = PkceHelper.ComputeUserAgentHash(userAgent1);
        var hash2 = PkceHelper.ComputeUserAgentHash(userAgent2);

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ComputeUserAgentHash_EmptyUA_ReturnsEmpty()
    {
        // Arrange
        var userAgent = string.Empty;

        // Act
        var hash = PkceHelper.ComputeUserAgentHash(userAgent);

        // Assert
        Assert.Empty(hash);
    }

    [Fact]
    public void ComputeUserAgentHash_NullUA_ReturnsEmpty()
    {
        // Act
        var hash = PkceHelper.ComputeUserAgentHash(null!);

        // Assert
        Assert.Empty(hash);
    }

    [Fact]
    public void PKCE_FullFlow_ValidatesCorrectly()
    {
        // This test simulates a full PKCE flow
        // Arrange
        var codeVerifier = PkceHelper.GenerateCodeVerifier();
        var codeChallenge = PkceHelper.ComputeCodeChallenge(codeVerifier);

        // Act - Simulate client sending challenge, then later sending verifier
        var recomputedChallenge = PkceHelper.ComputeCodeChallenge(codeVerifier);

        // Assert
        Assert.Equal(codeChallenge, recomputedChallenge);
    }
}
