namespace Storage.Application.DTOs;

public sealed record CompleteUploadRequest(
    string ChecksumSha256,
    long SizeBytes);
