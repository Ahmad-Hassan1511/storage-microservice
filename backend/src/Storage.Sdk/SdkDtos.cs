namespace Storage.Sdk;

public sealed record UploadFileRequest(
    string CategoryId,
    string OriginalFileName,
    string MimeType,
    long SizeBytes,
    string OwnerService);

public sealed record UploadFileResult(
    Guid FileId,
    string Status,
    string? DownloadUrl);

public sealed record InitiateUploadResponse(
    Guid FileId,
    string? UploadUrl,
    Dictionary<string, string> UploadHeaders,
    DateTimeOffset ExpiresAt,
    bool ProxyRequired,
    bool MultipartRequired);

public sealed record CompleteUploadResponse(Guid FileId, string Status);

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
