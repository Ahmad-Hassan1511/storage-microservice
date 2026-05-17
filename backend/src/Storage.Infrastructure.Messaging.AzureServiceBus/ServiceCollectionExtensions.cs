using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Storage.Application.Abstractions;

namespace Storage.Infrastructure.Messaging.AzureServiceBus;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAzureServiceBusMessaging(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddMassTransit(x =>
        {
            x.UsingAzureServiceBus((ctx, cfg) =>
            {
                cfg.Host(connectionString);
                cfg.ConfigureEndpoints(ctx);
            });
        });

        services.AddScoped<IEventBus, AzureServiceBusEventBus>();
        return services;
    }
}
