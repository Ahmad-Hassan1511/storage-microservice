namespace Storage.Application.DTOs;

public sealed record PatchFileRequest(
    string? OriginalFileName,
    Dictionary<string, string>? Tags,
    string? Visibility);
