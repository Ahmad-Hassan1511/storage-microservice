using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Storage.Application.Abstractions;

namespace Storage.Infrastructure.Storage.Wasabi;

public static class WasabiServiceCollectionExtensions
{
    public static IServiceCollection AddWasabiStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<WasabiOptions>(configuration.GetSection("Storage:Wasabi"));
        services.AddSingleton<IFileStorageProvider, WasabiStorageProvider>();
        return services;
    }
}
