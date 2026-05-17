namespace Storage.Sdk;

public interface IStorageClient
{
    /// <summary>
    /// Full upload cycle: buffer bytes, initiate, upload to object store or proxy, complete.
    /// </summary>
    Task<UploadFileResult> UploadAsync(Stream content, UploadFileRequest request, CancellationToken ct = default);

    /// <summary>Step 1: Initiate upload, get upload URL or proxy info.</summary>
    Task<InitiateUploadResponse> InitiateUploadAsync(UploadFileRequest request, string idempotencyKey, CancellationToken ct = default);

    /// <summary>Step 2 (proxy path): Forward raw bytes to the Storage API proxy endpoint.</summary>
    Task ProxyUploadAsync(Guid fileId, Stream content, string contentType, CancellationToken ct = default);

    /// <summary>Step 3: Confirm upload with checksum, trigger antivirus.</summary>
    Task<CompleteUploadResponse> CompleteUploadAsync(Guid fileId, string checksumSha256, long sizeBytes, CancellationToken ct = default);

    Task<GetFileResponse?> GetFileAsync(Guid fileId, CancellationToken ct = default);
    Task<Stream> DownloadContentAsync(Guid fileId, CancellationToken ct = default);
    Task DeleteFileAsync(Guid fileId, CancellationToken ct = default);

    /// <summary>Dev only: simulate antivirus approval. No-ops if the endpoint is absent (non-Development).</summary>
    Task DevMarkReadyAsync(Guid fileId, CancellationToken ct = default);
}
