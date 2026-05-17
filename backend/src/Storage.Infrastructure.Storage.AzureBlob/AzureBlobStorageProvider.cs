using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Options;
using Storage.Application.Abstractions;
using RangeHeader = System.Net.Http.Headers.RangeHeaderValue;

namespace Storage.Infrastructure.Storage.AzureBlob;

public sealed class AzureBlobOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public string DefaultContainerName { get; set; } = "storage";
}

public sealed class AzureBlobStorageProvider : IFileStorageProvider
{
    private readonly AzureBlobOptions _options;

    public StorageCapabilities Capabilities { get; } = new(
        SupportsPresignedUploadUrls: true,
        SupportsPresignedDownloadUrls: true,
        SupportsMultipartUpload: false,
        SupportsVersioning: false,
        SupportsServerSideEncryption: true,
        MaxObjectSizeBytes: 5L * 1024 * 1024 * 1024 * 1024); // 5 TB

    public AzureBlobStorageProvider(IOptions<AzureBlobOptions> options)
    {
        _options = options.Value;
    }

    private BlobClient GetBlobClient(string bucket, string key)
    {
        var container = new BlobContainerClient(_options.ConnectionString, bucket);
        return container.GetBlobClient(key);
    }

    public Task<StoragePutResult> GetUploadTargetAsync(StoragePutRequest request, CancellationToken ct)
    {
        var blob = GetBlobClient(request.Bucket, request.Key);
        var sasUri = blob.GenerateSasUri(BlobSasPermissions.Write | BlobSasPermissions.Create,
            DateTimeOffset.UtcNow.Add(request.PresignedUrlTtl));
        var headers = new Dictionary<string, string>
        {
            ["x-ms-blob-type"] = "BlockBlob",
            ["Content-Type"] = request.ContentType
        };
        return Task.FromResult(new StoragePutResult(sasUri.ToString(), headers, ProxyRequired: false));
    }

    public Task<StorageGetResult> GetDownloadTargetAsync(string bucket, string key, TimeSpan ttl, CancellationToken ct)
    {
        var blob = GetBlobClient(bucket, key);
        var sasUri = blob.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.Add(ttl));
        return Task.FromResult(new StorageGetResult(sasUri.ToString(), ProxyRequired: false));
    }

    public async Task<Stream> OpenReadStreamAsync(string bucket, string key, RangeHeader? range, CancellationToken ct)
    {
        var blob = GetBlobClient(bucket, key);
        BlobDownloadStreamingResult result;
        if (range?.Ranges.Count > 0)
        {
            var r = range.Ranges.First();
            long from = r.From ?? 0;
            long? to = r.To;
            var httpRange = to.HasValue
                ? new Azure.HttpRange(from, to.Value - from + 1)
                : new Azure.HttpRange(from);
            var downloadOptions = new BlobDownloadOptions { Range = httpRange };
            result = await blob.DownloadStreamingAsync(downloadOptions, ct);
        }
        else
        {
            result = await blob.DownloadStreamingAsync(cancellationToken: ct);
        }
        return result.Content;
    }

    public async Task WriteStreamAsync(string bucket, string key, Stream content, string contentType, CancellationToken ct)
    {
        var blob = GetBlobClient(bucket, key);
        await blob.UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
        }, ct);
    }

    public async Task DeleteAsync(string bucket, string key, CancellationToken ct)
    {
        var blob = GetBlobClient(bucket, key);
        await blob.DeleteIfExistsAsync(cancellationToken: ct);
    }

    public async Task<bool> ExistsAsync(string bucket, string key, CancellationToken ct)
    {
        var blob = GetBlobClient(bucket, key);
        var response = await blob.ExistsAsync(ct);
        return response.Value;
    }
}
