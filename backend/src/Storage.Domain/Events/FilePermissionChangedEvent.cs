namespace Storage.Domain.Events;

public sealed record FilePermissionChangedEvent(
    Guid FileId,
    Guid TenantId,
    string OwnerService,
    string PrincipalType,
    string PrincipalId,
    Guid EventId,
    DateTime OccurredAt) : DomainEvent(EventId, OccurredAt);
