namespace Storage.Domain.Events;

public sealed record FileReadyEvent(
    Guid FileId,
    Guid TenantId,
    string OwnerService,
    Guid EventId,
    DateTime OccurredAt) : DomainEvent(EventId, OccurredAt);
