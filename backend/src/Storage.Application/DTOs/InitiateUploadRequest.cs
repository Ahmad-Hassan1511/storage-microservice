namespace Storage.Application.DTOs;

public sealed record InitiateUploadRequest(
    string CategoryId,
    string OriginalFileName,
    string MimeType,
    long SizeBytes,
    string IdempotencyKey,
    string OwnerService);
