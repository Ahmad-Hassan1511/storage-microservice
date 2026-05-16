using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using Storage.Application.Abstractions;
using RangeHeader = System.Net.Http.Headers.RangeHeaderValue;

namespace Storage.Infrastructure.Storage.Wasabi;

public sealed class WasabiOptions
{
    public string ServiceUrl { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string Region { get; set; } = "us-east-1";
    public bool ForcePathStyle { get; set; } = true;
}

public sealed class WasabiStorageProvider : IFileStorageProvider, IAsyncDisposable
{
    private readonly AmazonS3Client _client;

    public StorageCapabilities Capabilities { get; } = new(
        SupportsPresignedUploadUrls: true,
        SupportsPresignedDownloadUrls: true,
        SupportsMultipartUpload: true,
        SupportsVersioning: false,
        SupportsServerSideEncryption: false,
        MaxObjectSizeBytes: 5L * 1024 * 1024 * 1024 * 1024); // 5 TB

    public WasabiStorageProvider(IOptions<WasabiOptions> options)
    {
        var o = options.Value;
        var config = new AmazonS3Config
        {
            ServiceURL = o.ServiceUrl,
            ForcePathStyle = o.ForcePathStyle
        };
        _client = new AmazonS3Client(o.AccessKey, o.SecretKey, config);
    }

    public async Task<StoragePutResult> GetUploadTargetAsync(StoragePutRequest request, CancellationToken ct)
    {
        var preSignedRequest = new GetPreSignedUrlRequest
        {
            BucketName = request.Bucket,
            Key = request.Key,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.Add(request.PresignedUrlTtl),
            ContentType = request.ContentType
        };
        var url = await _client.GetPreSignedURLAsync(preSignedRequest);
        return new StoragePutResult(url, new Dictionary<string, string> { ["Content-Type"] = request.ContentType }, ProxyRequired: false);
    }

    public async Task<StorageGetResult> GetDownloadTargetAsync(string bucket, string key, TimeSpan ttl, CancellationToken ct)
    {
        var preSignedRequest = new GetPreSignedUrlRequest
        {
            BucketName = bucket,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(ttl)
        };
        var url = await _client.GetPreSignedURLAsync(preSignedRequest);
        return new StorageGetResult(url, ProxyRequired: false);
    }

    public async Task<Stream> OpenReadStreamAsync(string bucket, string key, RangeHeader? range, CancellationToken ct)
    {
        var getRequest = new GetObjectRequest
        {
            BucketName = bucket,
            Key = key
        };
        if (range?.Ranges.Count > 0)
        {
            var r = range.Ranges.First();
            long from = r.From ?? 0;
            long? to = r.To;
            getRequest.ByteRange = to.HasValue
                ? new ByteRange($"bytes={from}-{to}")
                : new ByteRange($"bytes={from}-");
        }
        var response = await _client.GetObjectAsync(getRequest, ct);
        return response.ResponseStream;
    }

    public async Task WriteStreamAsync(string bucket, string key, Stream content, string contentType, CancellationToken ct)
    {
        var putRequest = new PutObjectRequest
        {
            BucketName = bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false
        };
        await _client.PutObjectAsync(putRequest, ct);
    }

    public async Task DeleteAsync(string bucket, string key, CancellationToken ct)
    {
        await _client.DeleteObjectAsync(bucket, key, ct);
    }

    public async Task<bool> ExistsAsync(string bucket, string key, CancellationToken ct)
    {
        try
        {
            await _client.GetObjectMetadataAsync(bucket, key, ct);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }
}
