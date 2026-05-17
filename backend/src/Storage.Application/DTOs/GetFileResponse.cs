namespace Storage.Application.DTOs;

public sealed record GetFileResponse(
    Guid FileId,
    string Status,
    string OriginalFileName,
    string MimeType,
    long SizeBytes,
    string? DownloadUrl,
    string? PreviewUrl,
    string? ThumbnailUrl,
    DateTime CreatedAt,
    DateTimeOffset? ExpiresAt);
