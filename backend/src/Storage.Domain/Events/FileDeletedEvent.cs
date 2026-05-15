namespace Storage.Domain.Events;

public sealed record FileDeletedEvent(
    Guid FileId,
    Guid TenantId,
    string OwnerService,
    Guid EventId,
    DateTime OccurredAt) : DomainEvent(EventId, OccurredAt);
