using IntelliFin.IdentityService.Configuration;
using IntelliFin.IdentityService.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace IntelliFin.IdentityService.Tests.Unit.Services;

public class PasswordServiceTests
{
    private readonly PasswordService _passwordService;
    private readonly PasswordConfiguration _config;

    public PasswordServiceTests()
    {
        _config = new PasswordConfiguration
        {
            MinLength = 8,
            MaxLength = 128,
            RequireUppercase = true,
            RequireLowercase = true,
            RequireDigit = true,
            RequireSpecialChar = true,
            RequiredUniqueChars = 2,
            SaltRounds = 10, // Lower for tests
            CommonPasswords = new[] { "password", "123456", "admin" }
        };

        var options = Options.Create(_config);
        var logger = new Mock<ILogger<PasswordService>>().Object;
        
        _passwordService = new PasswordService(options, logger);
    }

    [Fact]
    public async Task HashPasswordAsync_ValidPassword_ReturnsHashedPassword()
    {
        // Arrange
        const string password = "TestPassword123!";

        // Act
        var hashedPassword = await _passwordService.HashPasswordAsync(password);

        // Assert
        Assert.NotNull(hashedPassword);
        Assert.NotEmpty(hashedPassword);
        Assert.NotEqual(password, hashedPassword);
        Assert.True(hashedPassword.StartsWith("$2a$") || hashedPassword.StartsWith("$2b$"));
    }

    [Fact]
    public async Task VerifyPasswordAsync_CorrectPassword_ReturnsTrue()
    {
        // Arrange
        const string password = "TestPassword123!";
        var hashedPassword = await _passwordService.HashPasswordAsync(password);

        // Act
        var result = await _passwordService.VerifyPasswordAsync(password, hashedPassword);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task VerifyPasswordAsync_IncorrectPassword_ReturnsFalse()
    {
        // Arrange
        const string password = "TestPassword123!";
        const string wrongPassword = "WrongPassword456@";
        var hashedPassword = await _passwordService.HashPasswordAsync(password);

        // Act
        var result = await _passwordService.VerifyPasswordAsync(wrongPassword, hashedPassword);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task HashPasswordAsync_InvalidPassword_ThrowsArgumentException(string invalidPassword)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _passwordService.HashPasswordAsync(invalidPassword));
    }

    [Theory]
    [InlineData("short")]
    [InlineData("toolooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooong")]
    public async Task ValidatePasswordAsync_InvalidLength_ReturnsValidationErrors(string password)
    {
        // Act
        var result = await _passwordService.ValidatePasswordAsync(password);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("length"));
    }

    [Theory]
    [InlineData("testpassword123!")] // No uppercase
    [InlineData("TESTPASSWORD123!")] // No lowercase
    [InlineData("TestPassword!")] // No digit
    [InlineData("TestPassword123")] // No special char
    public async Task ValidatePasswordAsync_MissingCharacterTypes_ReturnsValidationErrors(string password)
    {
        // Act
        var result = await _passwordService.ValidatePasswordAsync(password);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count > 0);
    }

    [Fact]
    public async Task ValidatePasswordAsync_ValidPassword_ReturnsNoErrors()
    {
        // Arrange
        const string password = "StrongPassword123!";

        // Act
        var result = await _passwordService.ValidatePasswordAsync(password);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("password")]
    [InlineData("123456")]
    [InlineData("admin")]
    public async Task ValidatePasswordAsync_CommonPassword_ReturnsValidationError(string commonPassword)
    {
        // Act
        var result = await _passwordService.ValidatePasswordAsync(commonPassword);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("common") || e.Contains("compromised"));
    }

    [Fact]
    public async Task ValidatePasswordAsync_UsernameInPassword_ReturnsValidationError()
    {
        // Arrange
        const string username = "testuser";
        const string password = "TestuserPassword123!";

        // Act
        var result = await _passwordService.ValidatePasswordAsync(password, username);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("username"));
    }

    [Fact]
    public async Task CheckPasswordStrengthAsync_WeakPassword_ReturnsLowScore()
    {
        // Arrange
        const string weakPassword = "password";

        // Act
        var result = await _passwordService.CheckPasswordStrengthAsync(weakPassword);

        // Assert
        Assert.True(result.Score < 40);
        Assert.True(result.Strength <= PasswordStrength.Weak);
        Assert.NotEmpty(result.Suggestions);
    }

    [Fact]
    public async Task CheckPasswordStrengthAsync_StrongPassword_ReturnsHighScore()
    {
        // Arrange
        const string strongPassword = "MyVeryStr0ng&C0mpl3xP@ssw0rd!";

        // Act
        var result = await _passwordService.CheckPasswordStrengthAsync(strongPassword);

        // Assert
        Assert.True(result.Score >= 80);
        Assert.True(result.Strength >= PasswordStrength.Strong);
    }

    [Fact]
    public async Task GetPasswordPolicyAsync_ReturnsConfiguredPolicy()
    {
        // Act
        var policy = await _passwordService.GetPasswordPolicyAsync();

        // Assert
        Assert.Equal(_config.MinLength, policy.MinLength);
        Assert.Equal(_config.MaxLength, policy.MaxLength);
        Assert.Equal(_config.RequireUppercase, policy.RequireUppercase);
        Assert.Equal(_config.RequireLowercase, policy.RequireLowercase);
        Assert.Equal(_config.RequireDigit, policy.RequireDigit);
        Assert.Equal(_config.RequireSpecialChar, policy.RequireSpecialChar);
        Assert.NotEmpty(policy.Description);
    }
}