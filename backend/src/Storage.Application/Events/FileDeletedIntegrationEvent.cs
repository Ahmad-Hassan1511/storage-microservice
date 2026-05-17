namespace Storage.Application.Events;

public sealed record FileDeletedIntegrationEvent(
    Guid FileId,
    Guid TenantId,
    string OwnerService,
    bool HardDelete,
    Guid EventId,
    DateTime OccurredAt,
    string Source) : IntegrationEvent(EventId, OccurredAt, Source);
