using MassTransit;
using Storage.Application.Abstractions;
using Storage.Application.Events;

namespace Storage.Infrastructure.Messaging.RabbitMQ;

public sealed class RabbitMqEventBus : IEventBus
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IBusControl _busControl;

    public RabbitMqEventBus(IPublishEndpoint publishEndpoint, IBusControl busControl)
    {
        _publishEndpoint = publishEndpoint;
        _busControl = busControl;
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
