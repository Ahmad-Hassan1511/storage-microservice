namespace Storage.Domain.Events;

public abstract record DomainEvent(Guid EventId, DateTime OccurredAt);
