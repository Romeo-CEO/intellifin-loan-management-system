using IntelliFin.Shared.DomainModels.Data;
using IntelliFin.Shared.DomainModels.Entities;
using Microsoft.EntityFrameworkCore;

namespace IntelliFin.Tests.Unit.DomainModels;

public class LmsDbContextTests : IDisposable
{
    private readonly LmsDbContext _context;

    public LmsDbContextTests()
    {
        var options = new DbContextOptionsBuilder<LmsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new LmsDbContext(options);
    }

    [Fact]
    public void DbContext_Should_Have_All_Required_DbSets()
    {
        // Assert
        _context.Clients.Should().NotBeNull();
        _context.LoanApplications.Should().NotBeNull();
        _context.LoanProducts.Should().NotBeNull();
        _context.GLAccounts.Should().NotBeNull();
        _context.AuditEvents.Should().NotBeNull();
    }

    [Fact]
    public async Task DbContext_Should_Seed_Reference_Data()
    {
        // Act
        await _context.Database.EnsureCreatedAsync();

        // Assert - Loan Products
        var loanProducts = await _context.LoanProducts.ToListAsync();
        loanProducts.Should().HaveCount(3);
        loanProducts.Should().Contain(p => p.Code == "SALARY");
        loanProducts.Should().Contain(p => p.Code == "PAYROLL");
        loanProducts.Should().Contain(p => p.Code == "SME");

        // Assert - GL Accounts
        var glAccounts = await _context.GLAccounts.ToListAsync();
        glAccounts.Should().HaveCount(6);
        glAccounts.Should().Contain(a => a.AccountCode == "1000" && a.Name == "Cash and Bank");
        glAccounts.Should().Contain(a => a.AccountCode == "1100" && a.Name == "Loans Receivable");
    }

    [Fact]
    public async Task DbContext_Should_Have_Unique_Index_Configuration_For_NationalId()
    {
        // Arrange
        await _context.Database.EnsureCreatedAsync();

        // Act - Get the entity type configuration
        var entityType = _context.Model.FindEntityType(typeof(Client));
        var nationalIdProperty = entityType?.FindProperty(nameof(Client.NationalId));
        var indexes = entityType?.GetIndexes();

        // Assert - Verify unique index is configured (InMemory doesn't enforce constraints)
        nationalIdProperty.Should().NotBeNull();
        indexes.Should().NotBeNull();
        var uniqueIndex = indexes?.FirstOrDefault(i => i.IsUnique && i.Properties.Any(p => p.Name == nameof(Client.NationalId)));
        uniqueIndex.Should().NotBeNull("NationalId should have a unique index configured");
    }

    [Fact]
    public async Task DbContext_Should_Support_Client_LoanApplication_Relationship()
    {
        // Arrange
        await _context.Database.EnsureCreatedAsync();
        
        var client = new Client 
        { 
            Id = Guid.NewGuid(), 
            FirstName = "John", 
            LastName = "Doe", 
            NationalId = "123456789" 
        };
        
        var loanApplication = new LoanApplication
        {
            Id = Guid.NewGuid(),
            ClientId = client.Id,
            Amount = 10000,
            TermMonths = 12,
            ProductCode = "SALARY"
        };

        // Act
        _context.Clients.Add(client);
        _context.LoanApplications.Add(loanApplication);
        await _context.SaveChangesAsync();

        // Assert
        var savedClient = await _context.Clients
            .Include(c => c.LoanApplications)
            .FirstAsync(c => c.Id == client.Id);
            
        savedClient.LoanApplications.Should().HaveCount(1);
        savedClient.LoanApplications.First().Amount.Should().Be(10000);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
