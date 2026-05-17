using Microsoft.AspNetCore.Mvc;
using Storage.Application.DTOs;
using Storage.Application.Services;

namespace Storage.Api.Controllers;

[Route("v1/files")]
public class FilesController(
    UploadService uploadService,
    DownloadService downloadService,
    FileManagementService managementService) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ListFiles(
        [FromQuery] string? ownerService,
        [FromQuery] string? categoryId,
        [FromQuery] string? cursor,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var caller = BuildCaller();
        var query = new FileListQuery(caller.TenantId, ownerService, categoryId, cursor, Math.Clamp(pageSize, 1, 100), null);
        var result = await managementService.ListFilesAsync(query, ct);
        return result.IsSuccess ? Ok(result.Value) : MapError(result.Error!);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteFile(Guid id, CancellationToken ct)
    {
        var result = await managementService.SoftDeleteAsync(id, BuildCaller(), ct);
        return result.IsSuccess ? NoContent() : MapError(result.Error!);
    }

    [HttpPost]
    public async Task<IActionResult> InitiateUpload(
        [FromBody] InitiateUploadRequest body,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKeyHeader,
        CancellationToken ct)
    {
        var idempotencyKey = idempotencyKeyHeader ?? body.IdempotencyKey;
        var result = await uploadService.InitiateUploadAsync(body with { IdempotencyKey = idempotencyKey }, BuildCaller(), ct);
        return result.IsSuccess ? Ok(result.Value) : MapError(result.Error!);
    }

    [HttpPut("{id:guid}")]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> ProxyUpload(Guid id, CancellationToken ct)
    {
        var contentType = Request.ContentType ?? "application/octet-stream";
        var result = await uploadService.ProxyUploadAsync(id, Request.Body, contentType, BuildCaller(), ct);
        return result.IsSuccess ? NoContent() : MapError(result.Error!);
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> CompleteUpload(Guid id, [FromBody] CompleteUploadRequest body, CancellationToken ct)
    {
        var result = await uploadService.CompleteUploadAsync(id, body, BuildCaller(), ct);
        return result.IsSuccess ? Ok(result.Value) : MapError(result.Error!);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetFile(Guid id, CancellationToken ct)
    {
        var result = await downloadService.GetFileAsync(id, BuildCaller(), ct);
        return result.IsSuccess ? Ok(result.Value) : MapError(result.Error!);
    }

    [HttpGet("{id:guid}/content")]
    public async Task<IActionResult> GetFileContent(Guid id, CancellationToken ct)
    {
        var result = await downloadService.GetFileStreamAsync(id, BuildCaller(), ct);
        if (!result.IsSuccess) return MapError(result.Error!);
        return File(result.Value!, "application/octet-stream");
    }
}
