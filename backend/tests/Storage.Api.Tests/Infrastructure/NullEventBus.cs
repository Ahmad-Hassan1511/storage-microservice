using Storage.Application.Abstractions;
using Storage.Application.Events;

namespace Storage.Api.Tests.Infrastructure;

internal sealed class NullEventBus : IEventBus
{
    public Task PublishAsync<TEvent>(TEvent evt, CancellationToken ct) where TEvent : IntegrationEvent
        => Task.CompletedTask;

    public Task SubscribeAsync<TEvent, THandler>(CancellationToken ct)
        where TEvent : IntegrationEvent
        where THandler : IEventHandler<TEvent>
        => Task.CompletedTask;
}
