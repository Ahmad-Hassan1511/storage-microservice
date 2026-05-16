using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Storage.Application.Abstractions;
using Storage.Infrastructure.Cache.InMemory;
using Storage.Infrastructure.Storage.FileSystem;
using Testcontainers.MsSql;

namespace Storage.Api.Tests.Infrastructure;

public sealed class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private MsSqlContainer _sql = null!;
    private string _storageRoot = null!;

    public async ValueTask InitializeAsync()
    {
        _sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await _sql.StartAsync();
        _storageRoot = Path.Combine(Path.GetTempPath(), "api-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_storageRoot);
    }

    public new async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _sql.DisposeAsync();
        if (Directory.Exists(_storageRoot))
            Directory.Delete(_storageRoot, true);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Inject test-friendly config before DI runs
        builder.UseSetting("ConnectionStrings:DefaultConnection", _sql.GetConnectionString());
        builder.UseSetting("Storage:Provider", "filesystem");
        builder.UseSetting("Storage:FileSystem:RootPath", _storageRoot);
        builder.UseSetting("Cache:Provider", "inmemory");
        // Point messaging at rabbitmq but it won't be used — replaced below
        builder.UseSetting("Messaging:Provider", "rabbitmq");

        builder.ConfigureTestServices(services =>
        {
            // Replace event bus with a no-op stub so no broker is required
            services.RemoveAll<IEventBus>();
            services.AddSingleton<IEventBus, NullEventBus>();

            // Auth: accept all requests with a preset test identity
            services.AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
        });
    }
}
