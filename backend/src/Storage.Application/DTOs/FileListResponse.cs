namespace Storage.Application.DTOs;

public sealed record FileListResponse(
    IReadOnlyList<GetFileResponse> Items,
    string? NextCursor,
    int? TotalCount);
