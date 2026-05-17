using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Storage.Sdk;

namespace Documents.Controllers;

[ApiController]
[Route("api/documents")]
[Authorize]
public sealed class DocumentsController(
    DocumentsDbContext db,
    IStorageClient storage,
    IHostEnvironment env) : ControllerBase
{
    private Guid TenantId =>
        Guid.TryParse(User.FindFirst("tid")?.Value, out var g) ? g : Guid.Empty;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var docs = await db.Documents
            .Where(d => d.TenantId == TenantId && d.Status != "uploading")
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(ct);
        return Ok(docs);
    }

    /// <summary>
    /// Initiates an upload: creates a document record and calls Storage SDK server-side.
    /// Returns upload coordinates (presigned URL or proxy URL) — browser never touches Storage API.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> InitiateUpload([FromBody] InitiateDocumentUploadRequest req, CancellationToken ct)
    {
        var doc = new Document
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            Title = req.FileName,
            MimeType = req.MimeType,
            SizeBytes = req.SizeBytes,
            Status = "uploading",
            CreatedAt = DateTime.UtcNow,
        };
        db.Documents.Add(doc);

        var idempotencyKey = doc.Id.ToString("N");
        var init = await storage.InitiateUploadAsync(new UploadFileRequest(
            CategoryId: "document",
            OriginalFileName: req.FileName,
            MimeType: req.MimeType,
            SizeBytes: req.SizeBytes,
            OwnerService: "documents-service"), idempotencyKey, ct);

        doc.FileId = init.FileId;
        await db.SaveChangesAsync(ct);

        return Ok(new
        {
            id = doc.Id,
            fileId = init.FileId,
            uploadUrl = init.ProxyRequired ? null : init.UploadUrl,
            uploadHeaders = init.UploadHeaders,
            proxyRequired = init.ProxyRequired,
            proxyUploadUrl = init.ProxyRequired ? $"/api/documents/{doc.Id}/content" : null,
            completeUrl = $"/api/documents/{doc.Id}/complete",
            expiresAt = init.ExpiresAt,
        });
    }

    [HttpPut("{id:guid}/content")]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> ProxyUpload(Guid id, CancellationToken ct)
    {
        var doc = await db.Documents.FindAsync([id], ct);
        if (doc is null || doc.TenantId != TenantId || !doc.FileId.HasValue) return NotFound();

        var contentType = Request.ContentType ?? "application/octet-stream";
        await storage.ProxyUploadAsync(doc.FileId.Value, Request.Body, contentType, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> CompleteUpload(Guid id, [FromBody] CompleteDocumentUploadRequest req, CancellationToken ct)
    {
        var doc = await db.Documents.FindAsync([id], ct);
        if (doc is null || doc.TenantId != TenantId || !doc.FileId.HasValue) return NotFound();

        var result = await storage.CompleteUploadAsync(doc.FileId.Value, req.ChecksumSha256, req.SizeBytes, ct);

        if (env.IsDevelopment())
            await storage.DevMarkReadyAsync(doc.FileId.Value, ct);

        doc.Status = env.IsDevelopment() ? "ready" : result.Status;
        await db.SaveChangesAsync(ct);

        return Ok(new { id = doc.Id, fileId = doc.FileId, status = doc.Status });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var doc = await db.Documents.FindAsync([id], ct);
        if (doc is null || doc.TenantId != TenantId) return NotFound();
        return Ok(doc);
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var doc = await db.Documents.FindAsync([id], ct);
        if (doc is null || doc.TenantId != TenantId || !doc.FileId.HasValue) return NotFound();

        var stream = await storage.DownloadContentAsync(doc.FileId.Value, ct);
        return File(stream, doc.MimeType ?? "application/octet-stream", doc.Title);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var doc = await db.Documents.FindAsync([id], ct);
        if (doc is null || doc.TenantId != TenantId) return NotFound();

        if (doc.FileId.HasValue)
        {
            try { await storage.DeleteFileAsync(doc.FileId.Value, ct); }
            catch { }
        }

        db.Documents.Remove(doc);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
