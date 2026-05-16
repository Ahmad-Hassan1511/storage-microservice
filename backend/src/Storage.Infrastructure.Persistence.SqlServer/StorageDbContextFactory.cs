using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Storage.Infrastructure.Persistence.SqlServer.Interceptors;

namespace Storage.Infrastructure.Persistence.SqlServer;

public class StorageDbContextFactory : IDesignTimeDbContextFactory<StorageDbContext>
{
    public StorageDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<StorageDbContext>()
            .UseSqlServer(
                "Server=localhost;Database=StorageDb;Trusted_Connection=True;TrustServerCertificate=True;",
                sql => sql.EnableRetryOnFailure(3))
            .AddInterceptors(new SoftDeleteInterceptor())
            .Options;
        return new StorageDbContext(options);
    }
}
