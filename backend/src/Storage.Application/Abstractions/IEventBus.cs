using Storage.Application.Events;

namespace Storage.Application.Abstractions;

public interface IEventHandler<TEvent> where TEvent : IntegrationEvent
{
    Task HandleAsync(TEvent @event, CancellationToken ct);
}

public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct)
        where TEvent : IntegrationEvent;
    Task SubscribeAsync<TEvent, THandler>(CancellationToken ct)
        where TEvent : IntegrationEvent
        where THandler : IEventHandler<TEvent>;
}
