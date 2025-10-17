using IntelliFin.IdentityService.Models;
using IntelliFin.IdentityService.Validators;

namespace IntelliFin.IdentityService.Tests.Validators;

public class TenantValidatorsTests
{
    [Fact]
    public void TenantCreateRequestValidator_ValidRequest_PassesValidation()
    {
        // Arrange
        var validator = new TenantCreateRequestValidator();
        var request = new TenantCreateRequest
        {
            Name = "Test Tenant",
            Code = "test-tenant",
            Settings = "{\"key\":\"value\"}"
        };

        // Act
        var result = validator.Validate(request);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void TenantCreateRequestValidator_EmptyName_FailsValidation()
    {
        // Arrange
        var validator = new TenantCreateRequestValidator();
        var request = new TenantCreateRequest
        {
            Name = "",
            Code = "test-tenant"
        };

        // Act
        var result = validator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Name");
    }

    [Fact]
    public void TenantCreateRequestValidator_NameTooLong_FailsValidation()
    {
        // Arrange
        var validator = new TenantCreateRequestValidator();
        var request = new TenantCreateRequest
        {
            Name = new string('A', 201),
            Code = "test-tenant"
        };

        // Act
        var result = validator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Name" && e.ErrorMessage.Contains("200"));
    }

    [Fact]
    public void TenantCreateRequestValidator_EmptyCode_FailsValidation()
    {
        // Arrange
        var validator = new TenantCreateRequestValidator();
        var request = new TenantCreateRequest
        {
            Name = "Test Tenant",
            Code = ""
        };

        // Act
        var result = validator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Code");
    }

    [Fact]
    public void TenantCreateRequestValidator_CodeTooShort_FailsValidation()
    {
        // Arrange
        var validator = new TenantCreateRequestValidator();
        var request = new TenantCreateRequest
        {
            Name = "Test Tenant",
            Code = "ab"
        };

        // Act
        var result = validator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Code" && e.ErrorMessage.Contains("between 3 and 50"));
    }

    [Fact]
    public void TenantCreateRequestValidator_CodeTooLong_FailsValidation()
    {
        // Arrange
        var validator = new TenantCreateRequestValidator();
        var request = new TenantCreateRequest
        {
            Name = "Test Tenant",
            Code = new string('a', 51)
        };

        // Act
        var result = validator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Code" && e.ErrorMessage.Contains("between 3 and 50"));
    }

    [Theory]
    [InlineData("test-tenant")]
    [InlineData("test-tenant-123")]
    [InlineData("abc")]
    [InlineData("tenant-01")]
    [InlineData("123-456-789")]
    public void TenantCreateRequestValidator_ValidCodes_PassValidation(string code)
    {
        // Arrange
        var validator = new TenantCreateRequestValidator();
        var request = new TenantCreateRequest
        {
            Name = "Test Tenant",
            Code = code
        };

        // Act
        var result = validator.Validate(request);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("Test Tenant")] // Contains uppercase
    [InlineData("test_tenant")] // Contains underscore
    [InlineData("test.tenant")] // Contains dot
    [InlineData("test tenant")] // Contains space
    [InlineData("test@tenant")] // Contains special character
    public void TenantCreateRequestValidator_InvalidCodes_FailValidation(string code)
    {
        // Arrange
        var validator = new TenantCreateRequestValidator();
        var request = new TenantCreateRequest
        {
            Name = "Test Tenant",
            Code = code
        };

        // Act
        var result = validator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Code" && e.ErrorMessage.Contains("lowercase letters, numbers, and hyphens"));
    }

    [Fact]
    public void TenantCreateRequestValidator_NullSettings_PassesValidation()
    {
        // Arrange
        var validator = new TenantCreateRequestValidator();
        var request = new TenantCreateRequest
        {
            Name = "Test Tenant",
            Code = "test-tenant",
            Settings = null
        };

        // Act
        var result = validator.Validate(request);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void TenantCreateRequestValidator_EmptySettings_PassesValidation()
    {
        // Arrange
        var validator = new TenantCreateRequestValidator();
        var request = new TenantCreateRequest
        {
            Name = "Test Tenant",
            Code = "test-tenant",
            Settings = ""
        };

        // Act
        var result = validator.Validate(request);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void TenantCreateRequestValidator_ValidJsonSettings_PassesValidation()
    {
        // Arrange
        var validator = new TenantCreateRequestValidator();
        var request = new TenantCreateRequest
        {
            Name = "Test Tenant",
            Code = "test-tenant",
            Settings = "{\"key\":\"value\",\"nested\":{\"prop\":123}}"
        };

        // Act
        var result = validator.Validate(request);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void TenantCreateRequestValidator_InvalidJsonSettings_FailsValidation()
    {
        // Arrange
        var validator = new TenantCreateRequestValidator();
        var request = new TenantCreateRequest
        {
            Name = "Test Tenant",
            Code = "test-tenant",
            Settings = "{invalid json}"
        };

        // Act
        var result = validator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Settings" && e.ErrorMessage.Contains("valid JSON"));
    }

    [Fact]
    public void UserAssignmentRequestValidator_ValidRequest_PassesValidation()
    {
        // Arrange
        var validator = new UserAssignmentRequestValidator();
        var request = new UserAssignmentRequest
        {
            UserId = "user-123",
            Role = "Admin"
        };

        // Act
        var result = validator.Validate(request);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void UserAssignmentRequestValidator_EmptyUserId_FailsValidation()
    {
        // Arrange
        var validator = new UserAssignmentRequestValidator();
        var request = new UserAssignmentRequest
        {
            UserId = "",
            Role = "Admin"
        };

        // Act
        var result = validator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "UserId");
    }

    [Fact]
    public void UserAssignmentRequestValidator_NullRole_PassesValidation()
    {
        // Arrange
        var validator = new UserAssignmentRequestValidator();
        var request = new UserAssignmentRequest
        {
            UserId = "user-123",
            Role = null
        };

        // Act
        var result = validator.Validate(request);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void UserAssignmentRequestValidator_EmptyRole_PassesValidation()
    {
        // Arrange
        var validator = new UserAssignmentRequestValidator();
        var request = new UserAssignmentRequest
        {
            UserId = "user-123",
            Role = ""
        };

        // Act
        var result = validator.Validate(request);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void UserAssignmentRequestValidator_RoleTooLong_FailsValidation()
    {
        // Arrange
        var validator = new UserAssignmentRequestValidator();
        var request = new UserAssignmentRequest
        {
            UserId = "user-123",
            Role = new string('A', 101)
        };

        // Act
        var result = validator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Role" && e.ErrorMessage.Contains("100"));
    }
}
