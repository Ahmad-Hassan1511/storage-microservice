using RangeHeader = System.Net.Http.Headers.RangeHeaderValue;

namespace Storage.Application.Abstractions;

public sealed record StorageCapabilities(
    bool SupportsPresignedUploadUrls,
    bool SupportsPresignedDownloadUrls,
    bool SupportsMultipartUpload,
    bool SupportsVersioning,
    bool SupportsServerSideEncryption,
    long MaxObjectSizeBytes);

public sealed record StoragePutRequest(
    string Bucket, string Key, string ContentType,
    long SizeBytes, TimeSpan PresignedUrlTtl);

public sealed record StoragePutResult(
    string? PresignedUrl,
    Dictionary<string, string>? Headers,
    bool ProxyRequired);

public sealed record StorageGetResult(
    string? PresignedUrl,
    bool ProxyRequired);

public interface IFileStorageProvider
{
    StorageCapabilities Capabilities { get; }
    Task<StoragePutResult> GetUploadTargetAsync(StoragePutRequest request, CancellationToken ct);
    Task<StorageGetResult> GetDownloadTargetAsync(string bucket, string key, TimeSpan ttl, CancellationToken ct);
    Task<Stream> OpenReadStreamAsync(string bucket, string key, RangeHeader? range, CancellationToken ct);
    Task WriteStreamAsync(string bucket, string key, Stream content, string contentType, CancellationToken ct);
    Task DeleteAsync(string bucket, string key, CancellationToken ct);
    Task<bool> ExistsAsync(string bucket, string key, CancellationToken ct);
}
