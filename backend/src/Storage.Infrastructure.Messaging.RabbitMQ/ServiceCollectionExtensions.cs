using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Storage.Application.Abstractions;

namespace Storage.Infrastructure.Messaging.RabbitMQ;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRabbitMqMessaging(
        this IServiceCollection services,
        IConfiguration config)
    {
        var rabbitMqUri = config["RabbitMQ:Uri"] ?? "amqp://guest:guest@localhost:5672";

        services.AddMassTransit(x =>
        {
            x.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(rabbitMqUri);
                cfg.ConfigureEndpoints(ctx);
            });
        });

        services.AddSingleton<IEventBus, RabbitMqEventBus>();
        return services;
    }
}
