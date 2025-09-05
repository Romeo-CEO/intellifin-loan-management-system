using IntelliFin.Shared.DomainModels.Entities;

namespace IntelliFin.Tests.Unit.DomainModels;

public class ClientTests
{
    [Fact]
    public void Client_Should_Initialize_With_Default_Values()
    {
        // Act
        var client = new Client();

        // Assert
        client.Id.Should().Be(Guid.Empty);
        client.FirstName.Should().Be(string.Empty);
        client.LastName.Should().Be(string.Empty);
        client.NationalId.Should().Be(string.Empty);
        client.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        client.LoanApplications.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void Client_Should_Allow_Setting_Properties()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var firstName = "John";
        var lastName = "Doe";
        var nationalId = "123456789";
        var createdAt = DateTime.UtcNow.AddDays(-1);

        // Act
        var client = new Client
        {
            Id = clientId,
            FirstName = firstName,
            LastName = lastName,
            NationalId = nationalId,
            CreatedAtUtc = createdAt
        };

        // Assert
        client.Id.Should().Be(clientId);
        client.FirstName.Should().Be(firstName);
        client.LastName.Should().Be(lastName);
        client.NationalId.Should().Be(nationalId);
        client.CreatedAtUtc.Should().Be(createdAt);
    }

    [Fact]
    public void Client_Should_Support_LoanApplications_Collection()
    {
        // Arrange
        var client = new Client();
        var loanApplication = new LoanApplication
        {
            Id = Guid.NewGuid(),
            ClientId = client.Id,
            Amount = 10000,
            TermMonths = 12,
            ProductCode = "SALARY"
        };

        // Act
        client.LoanApplications.Add(loanApplication);

        // Assert
        client.LoanApplications.Should().HaveCount(1);
        client.LoanApplications.First().Should().Be(loanApplication);
    }
}
