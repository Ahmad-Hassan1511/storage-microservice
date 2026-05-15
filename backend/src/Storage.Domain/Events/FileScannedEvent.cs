namespace Storage.Domain.Events;

public sealed record FileScannedEvent(
    Guid FileId,
    Guid TenantId,
    string OwnerService,
    bool IsInfected,
    Guid EventId,
    DateTime OccurredAt) : DomainEvent(EventId, OccurredAt);
