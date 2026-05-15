using System.Security.Cryptography;
using System.Text;
using DomainFile = Storage.Domain.Entities.File;
using Storage.Application.Abstractions;
using Storage.Application.Common;
using Storage.Application.DTOs;
using Storage.Application.Events;
using Storage.Domain.Common;
using Storage.Domain.Enums;
using Storage.Domain.ValueObjects;

namespace Storage.Application.Services;

public class UploadService(
    IFileStorageProvider storage,
    ICacheProvider cache,
    IEventBus eventBus,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    private const string DefaultBucket = "files";
    private static readonly TimeSpan UploadUrlTtl = TimeSpan.FromHours(1);
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromHours(24);

    public async Task<Result<InitiateUploadResponse>> InitiateUploadAsync(
        InitiateUploadRequest req,
        CallerContext caller,
        CancellationToken ct)
    {
        var payloadHash = ComputePayloadHash(req.CategoryId, req.OriginalFileName, req.SizeBytes, req.OwnerService);
        var hashCacheKey = $"idempotency:{req.IdempotencyKey}:hash";
        var responseCacheKey = $"idempotency:{req.IdempotencyKey}:response";

        var storedHash = await cache.GetAsync<string>(hashCacheKey, ct);
        if (storedHash is not null)
        {
            if (storedHash != payloadHash)
                return Result<InitiateUploadResponse>.Failure(
                    new IdempotencyConflictError($"Idempotency key '{req.IdempotencyKey}' was used with a different payload."));
            var cachedResponse = await cache.GetAsync<InitiateUploadResponse>(responseCacheKey, ct);
            return Result<InitiateUploadResponse>.Success(cachedResponse!);
        }

        var category = await unitOfWork.Categories.GetByIdAsync(req.CategoryId, ct);
        if (category is null)
            return Result<InitiateUploadResponse>.Failure(
                new NotFoundError($"Category '{req.CategoryId}' not found."));

        if (category.AllowedOwnerServices.Length > 0 &&
            !category.AllowedOwnerServices.Contains(req.OwnerService, StringComparer.OrdinalIgnoreCase))
            return Result<InitiateUploadResponse>.Failure(
                new AccessDeniedError($"Owner service '{req.OwnerService}' is not allowed for category '{req.CategoryId}'."));

        if (req.SizeBytes > category.MaxSizeBytes)
            return Result<InitiateUploadResponse>.Failure(
                new PolicyViolationError(
                    $"File size {req.SizeBytes} exceeds the maximum allowed {category.MaxSizeBytes} bytes for category '{req.CategoryId}'.", 413));

        if (category.AllowedMimeTypes.Length > 0 &&
            !category.AllowedMimeTypes.Contains(req.MimeType, StringComparer.OrdinalIgnoreCase))
            return Result<InitiateUploadResponse>.Failure(
                new PolicyViolationError($"MIME type '{req.MimeType}' is not allowed for category '{req.CategoryId}'.", 415));

        if (category.AllowedExtensions.Length > 0)
        {
            var ext = System.IO.Path.GetExtension(req.OriginalFileName);
            if (!category.AllowedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
                return Result<InitiateUploadResponse>.Failure(
                    new PolicyViolationError($"Extension '{ext}' is not allowed for category '{req.CategoryId}'.", 415));
        }

        var file = DomainFile.Create(caller.TenantId, req.OwnerService, req.CategoryId, req.OriginalFileName, req.MimeType, req.SizeBytes);

        var multipartRequired = category.IsLargeFile ||
            (category.MultipartThresholdBytes.HasValue && req.SizeBytes > category.MultipartThresholdBytes.Value);

        var now = timeProvider.GetUtcNow();
        var storageKey = StorageKey.Create(caller.TenantId, DateOnly.FromDateTime(now.DateTime), file.Id);

        var putRequest = new StoragePutRequest(DefaultBucket, storageKey.Value, req.MimeType, req.SizeBytes, UploadUrlTtl);
        var putResult = await storage.GetUploadTargetAsync(putRequest, ct);

        await unitOfWork.Files.AddAsync(file, ct);
        await unitOfWork.SaveChangesAsync(ct);

        await eventBus.PublishAsync(new FileCreatedIntegrationEvent(
            file.Id, caller.TenantId, req.OwnerService, req.CategoryId,
            Guid.NewGuid(), now.DateTime, "upload-service"), ct);
        file.ClearDomainEvents();

        var response = new InitiateUploadResponse(
            FileId: file.Id,
            UploadUrl: putResult.PresignedUrl,
            UploadHeaders: putResult.Headers ?? [],
            ExpiresAt: now.Add(UploadUrlTtl),
            ProxyRequired: putResult.ProxyRequired,
            MultipartRequired: multipartRequired);

        await cache.SetAsync(hashCacheKey, payloadHash, IdempotencyTtl, ct);
        await cache.SetAsync(responseCacheKey, response, IdempotencyTtl, ct);

        return Result<InitiateUploadResponse>.Success(response);
    }

    public async Task<Result<CompleteUploadResponse>> CompleteUploadAsync(
        Guid fileId,
        CompleteUploadRequest req,
        CallerContext caller,
        CancellationToken ct)
    {
        var file = await unitOfWork.Files.GetByIdAsync(fileId, caller.TenantId, ct);
        if (file is null)
            return Result<CompleteUploadResponse>.Failure(new NotFoundError($"File {fileId} not found."));

        Checksum supplied;
        try
        {
            supplied = new Checksum(req.ChecksumSha256);
        }
        catch (InvalidChecksumException ex)
        {
            return Result<CompleteUploadResponse>.Failure(new PolicyViolationError(ex.Message, 400));
        }

        if (file.Checksum is null || file.Checksum.Value != supplied.Value)
            return Result<CompleteUploadResponse>.Failure(
                new ChecksumMismatchError("Supplied checksum does not match the stored checksum."));

        file.Transition(FileStatus.Scanning);

        await unitOfWork.SaveChangesAsync(ct);

        var now = timeProvider.GetUtcNow();
        await eventBus.PublishAsync(new FileUploadedIntegrationEvent(
            file.Id, file.TenantId, file.OwnerService, file.MimeType, file.SizeBytes,
            file.Tags.ToDictionary(t => t.Key, t => t.Value),
            Guid.NewGuid(), now.DateTime, "upload-service"), ct);

        file.ClearDomainEvents();

        return Result<CompleteUploadResponse>.Success(
            new CompleteUploadResponse(file.Id, file.Status.ToString().ToLowerInvariant()));
    }

    private static string ComputePayloadHash(string categoryId, string originalFileName, long sizeBytes, string ownerService)
    {
        var raw = $"{categoryId}|{originalFileName}|{sizeBytes}|{ownerService}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexStringLower(bytes);
    }
}
