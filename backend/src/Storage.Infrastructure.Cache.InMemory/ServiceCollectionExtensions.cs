using Microsoft.Extensions.DependencyInjection;
using Storage.Application.Abstractions;

namespace Storage.Infrastructure.Cache.InMemory;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInMemoryCacheProvider(
        this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<ICacheProvider, InMemoryCacheProvider>();
        return services;
    }
}
