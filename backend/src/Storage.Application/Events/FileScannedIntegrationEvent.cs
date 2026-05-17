namespace Storage.Application.Events;

public sealed record FileScannedIntegrationEvent(
    Guid FileId,
    Guid TenantId,
    string OwnerService,
    string ScanResult,
    Guid EventId,
    DateTime OccurredAt,
    string Source) : IntegrationEvent(EventId, OccurredAt, Source);
