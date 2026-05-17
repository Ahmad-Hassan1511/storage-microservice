using System.Net.Http.Json;
using System.Security.Cryptography;

namespace Storage.Sdk;

public sealed class StorageClient(HttpClient http, StorageClientOptions options) : IStorageClient
{
    public async Task<UploadFileResult> UploadAsync(
        Stream content,
        UploadFileRequest request,
        CancellationToken ct = default)
    {
        // Compute SHA-256 while buffering the stream for re-read
        byte[] bytes;
        string checksumHex;
        using (var ms = new MemoryStream())
        {
            await content.CopyToAsync(ms, ct);
            bytes = ms.ToArray();
        }
        checksumHex = Convert.ToHexStringLower(SHA256.HashData(bytes));

        var idempotencyKey = Guid.NewGuid().ToString("N");
        var initiateBody = new
        {
            categoryId = request.CategoryId,
            originalFileName = request.OriginalFileName,
            mimeType = request.MimeType,
            sizeBytes = request.SizeBytes,
            idempotencyKey,
            ownerService = request.OwnerService,
        };

        var initiateResp = await RetryAsync(async () =>
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/files");
            req.Headers.Add("Idempotency-Key", idempotencyKey);
            req.Content = JsonContent.Create(initiateBody);
            var resp = await http.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<InitiateUploadResponse>(ct)
                ?? throw new InvalidOperationException("Empty initiate response.");
        }, ct);

        if (!initiateResp.ProxyRequired && initiateResp.UploadUrl is not null)
        {
            // Direct PUT to presigned URL
            using var putClient = new HttpClient();
            using var putReq = new HttpRequestMessage(HttpMethod.Put, initiateResp.UploadUrl);
            putReq.Content = new ByteArrayContent(bytes);
            putReq.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(request.MimeType);
            foreach (var (k, v) in initiateResp.UploadHeaders)
                putReq.Headers.TryAddWithoutValidation(k, v);
            var putResp = await putClient.SendAsync(putReq, ct);
            putResp.EnsureSuccessStatusCode();
        }
        else
        {
            using var putReq = new HttpRequestMessage(HttpMethod.Put, $"/v1/files/{initiateResp.FileId}");
            putReq.Content = new ByteArrayContent(bytes);
            putReq.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(request.MimeType);
            var putResp = await http.SendAsync(putReq, ct);
            putResp.EnsureSuccessStatusCode();
        }

        var completeBody = new { checksumSha256 = checksumHex, sizeBytes = request.SizeBytes };
        var completeResp = await RetryAsync(async () =>
        {
            var resp = await http.PostAsJsonAsync($"/v1/files/{initiateResp.FileId}/complete", completeBody, ct);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<CompleteUploadResponse>(ct)
                ?? throw new InvalidOperationException("Empty complete response.");
        }, ct);

        return new UploadFileResult(completeResp.FileId, completeResp.Status, null);
    }

    public async Task<InitiateUploadResponse> InitiateUploadAsync(
        UploadFileRequest request, string idempotencyKey, CancellationToken ct = default)
    {
        var body = new
        {
            categoryId = request.CategoryId,
            originalFileName = request.OriginalFileName,
            mimeType = request.MimeType,
            sizeBytes = request.SizeBytes,
            idempotencyKey,
            ownerService = request.OwnerService,
        };
        return await RetryAsync(async () =>
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/files");
            req.Headers.Add("Idempotency-Key", idempotencyKey);
            req.Content = JsonContent.Create(body);
            var resp = await http.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<InitiateUploadResponse>(ct)
                ?? throw new InvalidOperationException("Empty initiate response.");
        }, ct);
    }

    public async Task ProxyUploadAsync(Guid fileId, Stream content, string contentType, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Put, $"/v1/files/{fileId}");
        req.Content = new StreamContent(content);
        req.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        var resp = await http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<CompleteUploadResponse> CompleteUploadAsync(
        Guid fileId, string checksumSha256, long sizeBytes, CancellationToken ct = default)
    {
        var body = new { checksumSha256, sizeBytes };
        return await RetryAsync(async () =>
        {
            var resp = await http.PostAsJsonAsync($"/v1/files/{fileId}/complete", body, ct);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<CompleteUploadResponse>(ct)
                ?? throw new InvalidOperationException("Empty complete response.");
        }, ct);
    }

    public async Task DevMarkReadyAsync(Guid fileId, CancellationToken ct = default)
    {
        var resp = await http.PostAsync($"/v1/dev/files/{fileId}/mark-ready", null, ct);
        // best-effort: 404 means non-Development environment, ignore
        if (resp.StatusCode != System.Net.HttpStatusCode.NotFound)
            resp.EnsureSuccessStatusCode();
    }

    public async Task<GetFileResponse?> GetFileAsync(Guid fileId, CancellationToken ct = default)
    {
        var resp = await http.GetAsync($"/v1/files/{fileId}", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<GetFileResponse>(ct);
    }

    public async Task<Stream> DownloadContentAsync(Guid fileId, CancellationToken ct = default)
    {
        var resp = await http.GetAsync($"/v1/files/{fileId}/content", HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStreamAsync(ct);
    }

    public async Task DeleteFileAsync(Guid fileId, CancellationToken ct = default)
    {
        var resp = await http.DeleteAsync($"/v1/files/{fileId}", ct);
        resp.EnsureSuccessStatusCode();
    }

    private async Task<T> RetryAsync<T>(Func<Task<T>> operation, CancellationToken ct)
    {
        var delay = options.InitialRetryDelay;
        for (var attempt = 0; attempt <= options.MaxRetries; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (HttpRequestException) when (attempt < options.MaxRetries)
            {
                await Task.Delay(delay, ct);
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2);
            }
        }
        // Final attempt (should not reach here, but satisfies compiler)
        return await operation();
    }
}
