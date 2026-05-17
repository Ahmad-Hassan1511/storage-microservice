namespace Storage.Application.Events;

public abstract record IntegrationEvent(
    Guid EventId,
    DateTime OccurredAt,
    string Source);
