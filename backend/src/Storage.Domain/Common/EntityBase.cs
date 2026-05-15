using Storage.Domain.Events;

namespace Storage.Domain.Common;

public abstract class EntityBase
{
    public Guid Id { get; protected set; }
    private readonly List<DomainEvent> _events = [];
    public IReadOnlyList<DomainEvent> DomainEvents => _events.AsReadOnly();
    protected void RaiseDomainEvent(DomainEvent @event) => _events.Add(@event);
    public void ClearDomainEvents() => _events.Clear();
}
