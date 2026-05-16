namespace Storage.Sdk;

public interface IStorageClient
{
    /// <summary>
    /// Initiates an upload, streams file bytes to the object store (or proxy),
    /// and completes the upload. Returns the fileId and final status.
    /// Handles multipart automatically for large files.
    /// </summary>
    Task<UploadFileResult> UploadAsync(
        Stream content,
        UploadFileRequest request,
        CancellationToken ct = default);

    Task<GetFileResponse?> GetFileAsync(Guid fileId, CancellationToken ct = default);
    Task<Stream> DownloadContentAsync(Guid fileId, CancellationToken ct = default);
    Task DeleteFileAsync(Guid fileId, CancellationToken ct = default);
}
