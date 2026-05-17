using Storage.Application.Abstractions;
using Storage.Application.Common;
using Storage.Application.DTOs;
using Storage.Domain.Entities;
using Storage.Domain.Enums;
using DomainFile = Storage.Domain.Entities.File;

namespace Storage.Application.Services;

internal sealed record ShareRecord(Guid FileId, Guid TenantId, DateTimeOffset ExpiresAt);

public sealed class FileManagementService(
    IFileStorageProvider storage,
    ICacheProvider cache,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Result<bool>> SoftDeleteAsync(Guid fileId, CallerContext caller, CancellationToken ct)
    {
        var file = await unitOfWork.Files.GetByIdAsync(fileId, caller.TenantId, ct);
        if (file is null)
            return Result<bool>.Failure(new NotFoundError($"File {fileId} not found."));

        file.Transition(FileStatus.Deleted);
        await unitOfWork.SaveChangesAsync(ct);
        await cache.RemoveAsync($"file:{fileId}", ct);
        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> HardDeleteAsync(Guid fileId, CallerContext caller, CancellationToken ct)
    {
        if (!caller.Scopes.Contains("storage.admin"))
            return Result<bool>.Failure(new AccessDeniedError("Requires storage.admin scope"));

        var file = await unitOfWork.Files.GetByIdAsync(fileId, caller.TenantId, ct);
        if (file is null)
            return Result<bool>.Failure(new NotFoundError($"File {fileId} not found."));

        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await storage.DeleteAsync("files", file.StorageKey!.Value, ct);
            await unitOfWork.Files.HardDeleteAsync(fileId, caller.TenantId, ct);
        }, ct);

        await cache.RemoveAsync($"file:{fileId}", ct);
        return Result<bool>.Success(true);
    }

    public async Task<Result<FileListResponse>> ListFilesAsync(FileListQuery query, CancellationToken ct)
    {
        var files = await unitOfWork.Files.ListAsync(query, ct);
        var hasMore = files.Count > query.PageSize;
        var page = hasMore ? files.Take(query.PageSize).ToList() : files.ToList();

        string? nextCursor = null;
        if (hasMore && page.Count > 0)
        {
            var lastId = page[^1].Id;
            nextCursor = Convert.ToBase64String(lastId.ToByteArray())
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        var items = page.Select(f => MapToResponse(f)).ToList();
        return Result<FileListResponse>.Success(new FileListResponse(items, nextCursor, null));
    }

    public async Task<Result<GetFileResponse>> PatchFileAsync(
        Guid fileId, PatchFileRequest req, CallerContext caller, CancellationToken ct)
    {
        var file = await unitOfWork.Files.GetByIdAsync(fileId, caller.TenantId, ct);
        if (file is null)
            return Result<GetFileResponse>.Failure(new NotFoundError($"File {fileId} not found."));

        if (req.OriginalFileName is not null)
            file.SetOriginalFileName(req.OriginalFileName);
        if (req.Visibility is not null)
            file.SetVisibility(Enum.Parse<Visibility>(req.Visibility, ignoreCase: true));
        if (req.Tags is not null)
            file.UpdateTags(req.Tags);

        await unitOfWork.SaveChangesAsync(ct);
        await cache.RemoveAsync($"file:{fileId}", ct);
        return Result<GetFileResponse>.Success(MapToResponse(file));
    }

    public async Task<Result<FileVersion>> CreateVersionAsync(
        Guid fileId, string storageKey, string checksumSha256, long sizeBytes,
        CallerContext caller, CancellationToken ct)
    {
        var file = await unitOfWork.Files.GetByIdAsync(fileId, caller.TenantId, ct);
        if (file is null)
            return Result<FileVersion>.Failure(new NotFoundError($"File {fileId} not found."));

        var versionNumber = file.Versions.Count + 1;
        var version = FileVersion.Create(
            fileId, versionNumber, storageKey, checksumSha256,
            sizeBytes, timeProvider.GetUtcNow().DateTime);

        await unitOfWork.FileVersions.AddAsync(version, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Result<FileVersion>.Success(version);
    }

    public async Task<Result<string>> GenerateShareLinkAsync(
        Guid fileId, TimeSpan ttl, CallerContext caller, CancellationToken ct)
    {
        var file = await unitOfWork.Files.GetByIdAsync(fileId, caller.TenantId, ct);
        if (file is null)
            return Result<string>.Failure(new NotFoundError($"File {fileId} not found."));
        if (file.Status != FileStatus.Ready)
            return Result<string>.Failure(new NotFoundError($"File {fileId} is not ready."));

        var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var cacheKey = $"share:{fileId}:{token}";
        var record = new ShareRecord(fileId, caller.TenantId, timeProvider.GetUtcNow().Add(ttl));
        await cache.SetAsync(cacheKey, record, ttl, ct);
        return Result<string>.Success(token);
    }

    private static GetFileResponse MapToResponse(DomainFile file) =>
        new(file.Id, file.Status.ToString().ToLowerInvariant(), file.OriginalFileName,
            file.MimeType, file.SizeBytes, null, null, null, file.CreatedAt, null);
}
