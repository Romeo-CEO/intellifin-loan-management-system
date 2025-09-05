using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace IntelliFin.Shared.DomainModels.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<LmsDbContext>
{
    public LmsDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<LmsDbContext>();
        // Default local dev connection (matches docker-compose)
        var connectionString = Environment.GetEnvironmentVariable("LMS_SQL_CONNECTION")
            ?? "Server=localhost,31433;Database=IntelliFinLms;User Id=sa;Password=Your_password123;TrustServerCertificate=True";
        builder.UseSqlServer(connectionString);
        return new LmsDbContext(builder.Options);
    }
}

