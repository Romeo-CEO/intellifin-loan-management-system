using IntelliFin.Shared.DomainModels.Entities;

namespace IntelliFin.Tests.Unit.DomainModels;

public class LoanApplicationTests
{
    [Fact]
    public void LoanApplication_Should_Initialize_With_Default_Values()
    {
        // Act
        var loanApplication = new LoanApplication();

        // Assert
        loanApplication.Id.Should().Be(Guid.Empty);
        loanApplication.ClientId.Should().Be(Guid.Empty);
        loanApplication.Amount.Should().Be(0);
        loanApplication.TermMonths.Should().Be(0);
        loanApplication.ProductCode.Should().Be(string.Empty);
        loanApplication.Status.Should().Be("Created");
        loanApplication.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        loanApplication.Client.Should().BeNull();
    }

    [Fact]
    public void LoanApplication_Should_Allow_Setting_Properties()
    {
        // Arrange
        var applicationId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var amount = 50000m;
        var termMonths = 24;
        var productCode = "PAYROLL";
        var status = "Approved";
        var createdAt = DateTime.UtcNow.AddDays(-1);

        // Act
        var loanApplication = new LoanApplication
        {
            Id = applicationId,
            ClientId = clientId,
            Amount = amount,
            TermMonths = termMonths,
            ProductCode = productCode,
            Status = status,
            CreatedAtUtc = createdAt
        };

        // Assert
        loanApplication.Id.Should().Be(applicationId);
        loanApplication.ClientId.Should().Be(clientId);
        loanApplication.Amount.Should().Be(amount);
        loanApplication.TermMonths.Should().Be(termMonths);
        loanApplication.ProductCode.Should().Be(productCode);
        loanApplication.Status.Should().Be(status);
        loanApplication.CreatedAtUtc.Should().Be(createdAt);
    }

    [Theory]
    [InlineData(1000, 6, "SALARY")]
    [InlineData(50000, 12, "PAYROLL")]
    [InlineData(100000, 24, "SME")]
    public void LoanApplication_Should_Accept_Valid_Product_Configurations(decimal amount, int termMonths, string productCode)
    {
        // Act
        var loanApplication = new LoanApplication
        {
            Amount = amount,
            TermMonths = termMonths,
            ProductCode = productCode
        };

        // Assert
        loanApplication.Amount.Should().Be(amount);
        loanApplication.TermMonths.Should().Be(termMonths);
        loanApplication.ProductCode.Should().Be(productCode);
    }
}
