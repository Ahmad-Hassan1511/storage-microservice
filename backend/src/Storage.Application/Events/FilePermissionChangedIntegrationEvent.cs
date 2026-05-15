namespace Storage.Application.Events;

public sealed record FilePermissionChangedIntegrationEvent(
    Guid FileId,
    Guid TenantId,
    string PrincipalType,
    string PrincipalId,
    string Action,
    Guid EventId,
    DateTime OccurredAt,
    string Source) : IntegrationEvent(EventId, OccurredAt, Source);
