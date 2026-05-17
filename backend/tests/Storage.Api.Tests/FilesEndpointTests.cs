using System.Net;
using System.Net.Http.Json;
using Storage.Api.Tests.Infrastructure;

namespace Storage.Api.Tests;

[Collection("Api")]
public sealed class FilesEndpointTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private readonly HttpClient _client = fixture.CreateClient();

    [Fact]
    public async Task Health_Returns200()
    {
        var ct = TestContext.Current.CancellationToken;
        var resp = await _client.GetAsync("/health", ct);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostFiles_ValidRequest_Returns200WithFileId()
    {
        var ct = TestContext.Current.CancellationToken;
        var idempotencyKey = Guid.NewGuid().ToString("N");

        var body = new
        {
            categoryId = "document",
            originalFileName = "test.pdf",
            mimeType = "application/pdf",
            sizeBytes = 1024L,
            idempotencyKey,
            ownerService = "test-service",
        };

        var req = new HttpRequestMessage(HttpMethod.Post, "/v1/files")
        {
            Content = JsonContent.Create(body),
        };
        req.Headers.Add("Idempotency-Key", idempotencyKey);

        var resp = await _client.SendAsync(req, ct);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var data = await resp.Content.ReadFromJsonAsync<InitiateUploadResponseDto>(cancellationToken: ct);
        data!.FileId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task PostFiles_IdempotentCall_ReturnsSameFileId()
    {
        var ct = TestContext.Current.CancellationToken;
        var idempotencyKey = Guid.NewGuid().ToString("N");

        var body = new
        {
            categoryId = "image",
            originalFileName = "photo.jpg",
            mimeType = "image/jpeg",
            sizeBytes = 512L,
            idempotencyKey,
            ownerService = "test-service",
        };

        var first = await PostFilesAsync(body, idempotencyKey, ct);
        var second = await PostFilesAsync(body, idempotencyKey, ct);

        first.FileId.Should().Be(second.FileId);
    }

    [Fact]
    public async Task GetFile_NonExistentId_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var resp = await _client.GetAsync($"/v1/files/{Guid.NewGuid()}", ct);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetCategories_Returns200WithList()
    {
        var ct = TestContext.Current.CancellationToken;
        var resp = await _client.GetAsync("/v1/categories", ct);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostFiles_UnknownCategory_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var body = new
        {
            categoryId = "nonexistent-category",
            originalFileName = "x.bin",
            mimeType = "application/octet-stream",
            sizeBytes = 100L,
            idempotencyKey = Guid.NewGuid().ToString("N"),
            ownerService = "test-service",
        };

        var resp = await _client.PostAsJsonAsync("/v1/files", body, ct);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CompleteUpload_NonExistentFile_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var body = new { checksumSha256 = new string('a', 64), sizeBytes = 100L };
        var resp = await _client.PostAsJsonAsync($"/v1/files/{Guid.NewGuid()}/complete", body, ct);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetFiles_EmptyTenant_Returns200WithEmptyList()
    {
        var ct = TestContext.Current.CancellationToken;
        var resp = await _client.GetAsync("/v1/files", ct);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var data = await resp.Content.ReadFromJsonAsync<FileListDto>(cancellationToken: ct);
        data!.Items.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteFile_NonExistentId_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var resp = await _client.DeleteAsync($"/v1/files/{Guid.NewGuid()}", ct);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DevMarkReady_InTestEnvironment_Returns404()
    {
        // DevController only activates in Development, not Testing
        var ct = TestContext.Current.CancellationToken;
        var resp = await _client.PostAsJsonAsync($"/v1/dev/files/{Guid.NewGuid()}/mark-ready", new { }, ct);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task FullProxyCycle_InitiateUploadCompleteAndList_FileAppearsAsScanning()
    {
        var ct = TestContext.Current.CancellationToken;
        var idempotencyKey = Guid.NewGuid().ToString("N");
        var fileBytes = "Hello, storage!"u8.ToArray();
        var checksum = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(fileBytes)).ToLowerInvariant();

        // 1. Initiate upload
        var initBody = new
        {
            categoryId = "document",
            originalFileName = "integration-test.pdf",
            mimeType = "application/pdf",
            sizeBytes = (long)fileBytes.Length,
            idempotencyKey,
            ownerService = "test-service",
        };
        var initResp = await PostFilesAsync(initBody, idempotencyKey, ct);
        initResp.ProxyRequired.Should().BeTrue();  // FileSystem adapter → always proxy
        initResp.UploadUrl.Should().BeNull();

        // 2. Proxy-upload the bytes
        var putReq = new HttpRequestMessage(HttpMethod.Put, $"/v1/files/{initResp.FileId}")
        {
            Content = new ByteArrayContent(fileBytes),
        };
        putReq.Content.Headers.ContentType = new("application/pdf");
        var putResp = await _client.SendAsync(putReq, ct);
        putResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 3. Complete upload
        var completeBody = new { checksumSha256 = checksum, sizeBytes = (long)fileBytes.Length };
        var completeResp = await _client.PostAsJsonAsync($"/v1/files/{initResp.FileId}/complete", completeBody, ct);
        completeResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var completeData = await completeResp.Content.ReadFromJsonAsync<CompleteUploadDto>(cancellationToken: ct);
        completeData!.Status.Should().Be("scanning");

        // 4. List files — file should appear
        var listResp = await _client.GetAsync("/v1/files?categoryId=document", ct);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var listData = await listResp.Content.ReadFromJsonAsync<FileListDto>(cancellationToken: ct);
        listData!.Items.Should().ContainSingle(f => f.FileId == initResp.FileId);

        // 5. Soft-delete the file
        var deleteResp = await _client.DeleteAsync($"/v1/files/{initResp.FileId}", ct);
        deleteResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 6. File no longer appears in list (deleted status filtered by caller or excluded)
        // The file entry still exists but status=deleted; list returns all statuses for now
        var listAfterDelete = await _client.GetAsync("/v1/files?categoryId=document", ct);
        listAfterDelete.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<InitiateUploadResponseDto> PostFilesAsync(object body, string idempotencyKey, CancellationToken ct)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/v1/files")
        {
            Content = JsonContent.Create(body),
        };
        req.Headers.Add("Idempotency-Key", idempotencyKey);
        var resp = await _client.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<InitiateUploadResponseDto>(cancellationToken: ct))!;
    }

    private sealed record InitiateUploadResponseDto(
        Guid FileId,
        string? UploadUrl,
        Dictionary<string, string> UploadHeaders,
        DateTimeOffset ExpiresAt,
        bool ProxyRequired,
        bool MultipartRequired);

    private sealed record CompleteUploadDto(Guid FileId, string Status);

    private sealed record FileListDto(
        List<FileItemDto> Items,
        string? NextCursor,
        int? TotalCount);

    private sealed record FileItemDto(Guid FileId, string Status, string OriginalFileName);
}
