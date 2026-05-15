namespace Storage.Application.Events;

public sealed record FileCreatedIntegrationEvent(
    Guid FileId,
    Guid TenantId,
    string OwnerService,
    string CategoryId,
    Guid EventId,
    DateTime OccurredAt,
    string Source) : IntegrationEvent(EventId, OccurredAt, Source);
