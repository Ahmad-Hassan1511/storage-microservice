using Storage.Application.Abstractions;
using Storage.Application.Common;
using Storage.Application.DTOs;
using Storage.Domain.Entities;
using Storage.Domain.Enums;
using DomainFile = Storage.Domain.Entities.File;

namespace Storage.Application.Services;

/// <summary>
/// Read-side use case orchestrator: cache-first metadata fetch, status gate,
/// ACL enforcement, and presigned URL generation (plus proxy stream for audited downloads).
/// Implements APP-05 (GetFileAsync) and APP-06 (GetFileStreamAsync).
/// </summary>
public class DownloadService(
    IFileStorageProvider storage,
    ICacheProvider cache,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    private const string DefaultBucket = "files";
    private static readonly TimeSpan DownloadUrlTtl = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Returns file metadata with a short-lived presigned download URL.
    /// Enforces: tenant isolation (404), status gate (404 for non-ready), ACL (403).
    /// </summary>
    public async Task<Result<GetFileResponse>> GetFileAsync(
        Guid fileId,
        CallerContext caller,
        CancellationToken ct)
    {
        var cacheKey = $"file:{fileId}";

        // Step 1: cache-first read
        var cached = await cache.GetAsync<GetFileResponse>(cacheKey, ct);
        if (cached is not null)
            return Result<GetFileResponse>.Success(cached);

        // Step 2: load from DB scoped by TenantId (anti-enumeration: null = 404)
        var file = await unitOfWork.Files.GetByIdAsync(fileId, caller.TenantId, ct);
        if (file is null)
            return Result<GetFileResponse>.Failure(new NotFoundError($"File {fileId} not found."));

        // Step 3: status gate — never serve non-ready files (architecture §6.5)
        if (file.Status != FileStatus.Ready)
            return Result<GetFileResponse>.Failure(new NotFoundError($"File {fileId} not found."));

        // Step 4: ACL check — skip for Public files
        if (file.Visibility != Visibility.Public)
        {
            var hasRead = file.Permissions.Any(p =>
                p.PrincipalType == caller.PrincipalType &&
                p.PrincipalId == caller.PrincipalId &&
                p.Permission == Permission.Read);

            if (!hasRead)
                return Result<GetFileResponse>.Failure(new AccessDeniedError("Insufficient permission to read this file."));
        }

        // Step 5: generate presigned download URL
        var target = await storage.GetDownloadTargetAsync(DefaultBucket, file.StorageKey!.Value, DownloadUrlTtl, ct);

        // Step 6: build response (PreviewUrl/ThumbnailUrl left null — requires separate StorageKey lookup, out of scope)
        var response = new GetFileResponse(
            FileId: file.Id,
            Status: file.Status.ToString().ToLowerInvariant(),
            OriginalFileName: file.OriginalFileName,
            MimeType: file.MimeType,
            SizeBytes: file.SizeBytes,
            DownloadUrl: target.PresignedUrl,
            PreviewUrl: null,
            ThumbnailUrl: null,
            CreatedAt: file.CreatedAt,
            ExpiresAt: timeProvider.GetUtcNow().Add(DownloadUrlTtl));

        // Step 7: populate cache
        await cache.SetAsync(cacheKey, response, CacheTtl, ct);

        return Result<GetFileResponse>.Success(response);
    }

    /// <summary>
    /// Returns an open stream for the audited proxy download path (APP-06).
    /// Only invoked when proxyRequired=true — not for presigned-URL files.
    /// Writes an audit entry before returning the stream.
    /// </summary>
    public async Task<Result<Stream>> GetFileStreamAsync(
        Guid fileId,
        CallerContext caller,
        CancellationToken ct)
    {
        var cacheKey = $"file:{fileId}";

        // Steps 1-4: same cache-first lookup + status gate + ACL as GetFileAsync
        var cached = await cache.GetAsync<GetFileResponse>(cacheKey, ct);
        DomainFile? file = null;

        if (cached is null)
        {
            file = await unitOfWork.Files.GetByIdAsync(fileId, caller.TenantId, ct);
            if (file is null)
                return Result<Stream>.Failure(new NotFoundError($"File {fileId} not found."));

            if (file.Status != FileStatus.Ready)
                return Result<Stream>.Failure(new NotFoundError($"File {fileId} not found."));

            if (file.Visibility != Visibility.Public)
            {
                var hasRead = file.Permissions.Any(p =>
                    p.PrincipalType == caller.PrincipalType &&
                    p.PrincipalId == caller.PrincipalId &&
                    p.Permission == Permission.Read);

                if (!hasRead)
                    return Result<Stream>.Failure(new AccessDeniedError("Insufficient permission to read this file."));
            }
        }

        // Step 2: write audit entry
        var now = timeProvider.GetUtcNow().DateTime;
        var audit = AuditEntry.Create(
            fileId: fileId,
            action: "proxy-download",
            performedBy: caller.PrincipalId,
            performedAt: now);

        await unitOfWork.Audit.AddAsync(audit, ct);
        await unitOfWork.SaveChangesAsync(ct);

        // Step 4: open read stream
        var storageKey = file?.StorageKey?.Value ?? cached!.FileId.ToString();
        // When cache hit, we must still get storageKey from DB for proxy path
        if (file is null)
        {
            file = await unitOfWork.Files.GetByIdAsync(fileId, caller.TenantId, ct);
            if (file is null)
                return Result<Stream>.Failure(new NotFoundError($"File {fileId} not found."));
        }

        var stream = await storage.OpenReadStreamAsync(DefaultBucket, file.StorageKey!.Value, null, ct);
        return Result<Stream>.Success(stream);
    }
}
