namespace Storage.Application.DTOs;

public sealed record InitiateUploadResponse(
    Guid FileId,
    string? UploadUrl,
    Dictionary<string, string> UploadHeaders,
    DateTimeOffset ExpiresAt,
    bool ProxyRequired,
    bool MultipartRequired);
