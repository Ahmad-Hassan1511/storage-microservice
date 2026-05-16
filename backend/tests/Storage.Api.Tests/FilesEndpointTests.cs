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
}
