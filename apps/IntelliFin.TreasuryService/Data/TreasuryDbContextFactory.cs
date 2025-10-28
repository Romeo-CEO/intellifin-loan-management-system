using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace IntelliFin.TreasuryService.Data;

public class TreasuryDbContextFactory : IDesignTimeDbContextFactory<TreasuryDbContext>
{
    public TreasuryDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TreasuryDbContext>();

        // Use Windows authentication with the IntelliFinLms_Dev database
        optionsBuilder.UseSqlServer(
            "Server=MOFIN-MFL0320\\SQLEXPRESS;Database=IntelliFinLms_Dev;Trusted_Connection=true;MultipleActiveResultSets=true;TrustServerCertificate=true;",
            sqlOptions => sqlOptions.MigrationsAssembly("IntelliFin.TreasuryService"));

        return new TreasuryDbContext(optionsBuilder.Options);
    }
}
