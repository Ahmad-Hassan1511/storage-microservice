using Microsoft.Extensions.Options;
using Storage.Application.Abstractions;
using RangeHeader = System.Net.Http.Headers.RangeHeaderValue;

namespace Storage.Infrastructure.Storage.FileSystem;

public sealed class FileSystemOptions
{
    public string BasePath { get; set; } = Path.Combine(Path.GetTempPath(), "storage-microservice");
}

public sealed class FileSystemStorageProvider : IFileStorageProvider
{
    private readonly string _basePath;

    public StorageCapabilities Capabilities { get; } = new(
        SupportsPresignedUploadUrls: false,
        SupportsPresignedDownloadUrls: false,
        SupportsMultipartUpload: false,
        SupportsVersioning: false,
        SupportsServerSideEncryption: false,
        MaxObjectSizeBytes: long.MaxValue);

    public FileSystemStorageProvider(IOptions<FileSystemOptions> options)
    {
        _basePath = options.Value.BasePath;
        Directory.CreateDirectory(_basePath);
    }

    private string ResolvePath(string bucket, string key)
    {
        // Sanitise key to prevent path traversal
        var combined = Path.GetFullPath(Path.Combine(_basePath, bucket, key));
        if (!combined.StartsWith(Path.GetFullPath(_basePath), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Path traversal detected.");
        return combined;
    }

    public Task<StoragePutResult> GetUploadTargetAsync(StoragePutRequest request, CancellationToken ct)
    {
        // FileSystem does not support presigned URLs — caller must use WriteStreamAsync
        return Task.FromResult(new StoragePutResult(null, null, ProxyRequired: true));
    }

    public Task<StorageGetResult> GetDownloadTargetAsync(string bucket, string key, TimeSpan ttl, CancellationToken ct)
    {
        return Task.FromResult(new StorageGetResult(null, ProxyRequired: true));
    }

    public async Task<Stream> OpenReadStreamAsync(string bucket, string key, RangeHeader? range, CancellationToken ct)
    {
        var path = ResolvePath(bucket, key);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Object not found: {bucket}/{key}");

        if (range?.Ranges.Count > 0)
        {
            var r = range.Ranges.First();
            var fileInfo = new FileInfo(path);
            long fileLength = fileInfo.Length;
            long from = r.From ?? 0;
            long to = r.To ?? (fileLength - 1);
            long length = to - from + 1;

            // Read bounded range into MemoryStream
            await using var fs = new System.IO.FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            fs.Seek(from, SeekOrigin.Begin);
            var buffer = new byte[length];
            int totalRead = 0;
            while (totalRead < length)
            {
                int read = await fs.ReadAsync(buffer.AsMemory(totalRead, (int)(length - totalRead)), ct);
                if (read == 0) break;
                totalRead += read;
            }
            return new MemoryStream(buffer, 0, totalRead, writable: false);
        }

        return new System.IO.FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    public async Task WriteStreamAsync(string bucket, string key, Stream content, string contentType, CancellationToken ct)
    {
        var path = ResolvePath(bucket, key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var fs = new System.IO.FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(fs, ct);
    }

    public Task DeleteAsync(string bucket, string key, CancellationToken ct)
    {
        var path = ResolvePath(bucket, key);
        if (File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string bucket, string key, CancellationToken ct)
    {
        var path = ResolvePath(bucket, key);
        return Task.FromResult(File.Exists(path));
    }
}
