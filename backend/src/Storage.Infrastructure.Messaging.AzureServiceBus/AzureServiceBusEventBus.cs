using MassTransit;
using Storage.Application.Abstractions;
using Storage.Application.Events;

namespace Storage.Infrastructure.Messaging.AzureServiceBus;

public sealed class AzureServiceBusEventBus : IEventBus
{
    private readonly IPublishEndpoint _publishEndpoint;

    public AzureServiceBusEventBus(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct)
        where TEvent : IntegrationEvent
    {
        await _publishEndpoint.Publish(@event, ct).ConfigureAwait(false);
    }

    public Task SubscribeAsync<TEvent, THandler>(CancellationToken ct)
        where TEvent : IntegrationEvent
        where THandler : IEventHandler<TEvent>
    {
        // MassTransit handles consumer registration at startup via ConfigureEndpoints.
        // Runtime subscription is a no-op; consumers are wired during DI configuration.
        return Task.CompletedTask;
    }
}
