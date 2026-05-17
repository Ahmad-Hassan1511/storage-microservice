using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Storage.Infrastructure.Persistence.SqlServer;
using Storage.Infrastructure.Persistence.SqlServer.Extensions;
using Storage.Infrastructure.Persistence.SqlServer.Interceptors;
using Testcontainers.MsSql;

namespace Storage.Infrastructure.Persistence.SqlServer.Tests.Infrastructure;

public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        await using var ctx = CreateDbContext();
        await ctx.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
        => await _container.DisposeAsync();

    public StorageDbContext CreateDbContext()
    {
        var interceptor = new SoftDeleteInterceptor();
        var options = new DbContextOptionsBuilder<StorageDbContext>()
            .UseSqlServer(ConnectionString, sql => sql.EnableRetryOnFailure(3))
            .AddInterceptors(interceptor)
            .Options;
        return new StorageDbContext(options);
    }

    public IServiceScope CreateScope()
    {
        var services = new ServiceCollection();
        services.AddSqlServerPersistence(ConnectionString);
        var provider = services.BuildServiceProvider();
        return provider.CreateScope();
    }
}
