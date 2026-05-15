namespace Storage.Application.DTOs;

public sealed record CompleteUploadResponse(
    Guid FileId,
    string Status);
