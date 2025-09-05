using IntelliFin.Shared.Infrastructure.Messaging.Contracts;

namespace IntelliFin.Tests.Unit.Messaging;

public class LoanApplicationCreatedTests
{
    [Fact]
    public void LoanApplicationCreated_Should_Initialize_With_Provided_Values()
    {
        // Arrange
        var applicationId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var amount = 50000m;
        var termMonths = 12;
        var productCode = "PAYROLL";
        var createdAt = DateTime.UtcNow;

        // Act
        var message = new LoanApplicationCreated(
            applicationId,
            clientId,
            amount,
            termMonths,
            productCode,
            createdAt
        );

        // Assert
        message.ApplicationId.Should().Be(applicationId);
        message.ClientId.Should().Be(clientId);
        message.Amount.Should().Be(amount);
        message.TermMonths.Should().Be(termMonths);
        message.ProductCode.Should().Be(productCode);
        message.CreatedAtUtc.Should().Be(createdAt);
    }

    [Fact]
    public void LoanApplicationCreated_Should_Support_Equality_Comparison()
    {
        // Arrange
        var applicationId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var amount = 50000m;
        var termMonths = 12;
        var productCode = "PAYROLL";
        var createdAt = DateTime.UtcNow;

        var message1 = new LoanApplicationCreated(applicationId, clientId, amount, termMonths, productCode, createdAt);
        var message2 = new LoanApplicationCreated(applicationId, clientId, amount, termMonths, productCode, createdAt);

        // Act & Assert
        message1.Should().Be(message2);
        message1.GetHashCode().Should().Be(message2.GetHashCode());
    }

    [Fact]
    public void LoanApplicationCreated_Should_Support_Deconstruction()
    {
        // Arrange
        var applicationId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var amount = 50000m;
        var termMonths = 12;
        var productCode = "PAYROLL";
        var createdAt = DateTime.UtcNow;

        var message = new LoanApplicationCreated(applicationId, clientId, amount, termMonths, productCode, createdAt);

        // Act
        var (appId, cId, amt, term, code, created) = message;

        // Assert
        appId.Should().Be(applicationId);
        cId.Should().Be(clientId);
        amt.Should().Be(amount);
        term.Should().Be(termMonths);
        code.Should().Be(productCode);
        created.Should().Be(createdAt);
    }
}
