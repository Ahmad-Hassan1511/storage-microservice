namespace Storage.Domain.Events;

public sealed record FileCreatedEvent(
    Guid FileId,
    Guid TenantId,
    string OwnerService,
    string CategoryId,
    Guid EventId,
    DateTime OccurredAt) : DomainEvent(EventId, OccurredAt);
