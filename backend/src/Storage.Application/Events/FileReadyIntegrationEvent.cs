namespace Storage.Application.Events;

public sealed record FileReadyIntegrationEvent(
    Guid FileId,
    Guid TenantId,
    string OwnerService,
    string CategoryId,
    string MimeType,
    long SizeBytes,
    Guid EventId,
    DateTime OccurredAt,
    string Source) : IntegrationEvent(EventId, OccurredAt, Source);
