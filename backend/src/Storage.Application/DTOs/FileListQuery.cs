namespace Storage.Application.DTOs;

public sealed record FileListQuery(
    Guid TenantId,
    string? OwnerService,
    string? CategoryId,
    string? Cursor,
    int PageSize,
    Dictionary<string, string>? Tags);
