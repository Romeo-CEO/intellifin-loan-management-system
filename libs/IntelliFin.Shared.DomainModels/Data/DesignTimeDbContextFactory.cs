using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace IntelliFin.Shared.DomainModels.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<LmsDbContext>
{
    public LmsDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<LmsDbContext>();
        // Default local dev connection
        var connectionString = Environment.GetEnvironmentVariable("LMS_SQL_CONNECTION")
            ?? "Server=MOFIN-MFL0320\\SQLEXPRESS;Database=IntelliFinLms_Dev;Integrated Security=True;TrustServerCertificate=True";
        builder.UseSqlServer(connectionString);
        return new LmsDbContext(builder.Options);
    }
}

