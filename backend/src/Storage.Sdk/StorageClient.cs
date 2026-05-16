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
            // Proxy path: PUT directly to the API /v1/files/{id}/content not yet implemented;
            // fall through to complete — the API will fetch the bytes from a pre-staged location
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
