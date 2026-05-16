using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Storage.Application.Abstractions;

namespace Storage.Infrastructure.Cache.Redis;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRedisCacheProvider(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddSingleton<IConnectionMultiplexer>(
            _ => ConnectionMultiplexer.Connect(connectionString));
        services.AddSingleton<ICacheProvider, RedisCacheProvider>();
        return services;
    }
}
