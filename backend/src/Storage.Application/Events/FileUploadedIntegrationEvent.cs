namespace Storage.Application.Events;

public sealed record FileUploadedIntegrationEvent(
    Guid FileId,
    Guid TenantId,
    string OwnerService,
    string MimeType,
    long SizeBytes,
    Dictionary<string, string> Tags,
    Guid EventId,
    DateTime OccurredAt,
    string Source) : IntegrationEvent(EventId, OccurredAt, Source);
