using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Storage.Sdk;

namespace Profile.Controllers;

[ApiController]
[Route("api/profiles")]
[Authorize]
public sealed class ProfileController(
    ProfileDbContext db,
    IStorageClient storage,
    IHostEnvironment env) : ControllerBase
{
    private string UserId =>
        User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value
        ?? "anonymous";

    [HttpGet("me")]
    public async Task<IActionResult> GetMe(CancellationToken ct)
    {
        var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == UserId, ct);
        if (profile is null)
            return Ok(new { userId = UserId, avatarStatus = (string?)null, avatarFileId = (Guid?)null });
        return Ok(profile);
    }

    /// <summary>
    /// Initiates avatar upload via Storage SDK server-side.
    /// Returns upload coordinates — browser never touches Storage API directly.
    /// </summary>
    [HttpPost("me/avatar")]
    public async Task<IActionResult> InitiateAvatarUpload([FromBody] InitiateAvatarUploadRequest req, CancellationToken ct)
    {
        var idempotencyKey = Guid.NewGuid().ToString("N");
        var init = await storage.InitiateUploadAsync(new UploadFileRequest(
            CategoryId: "image",
            OriginalFileName: req.FileName,
            MimeType: req.MimeType,
            SizeBytes: req.SizeBytes,
            OwnerService: "profile-service"), idempotencyKey, ct);

        return Ok(new
        {
            id = init.FileId.ToString(),
            fileId = init.FileId,
            uploadUrl = init.ProxyRequired ? null : init.UploadUrl,
            uploadHeaders = init.UploadHeaders,
            proxyRequired = init.ProxyRequired,
            proxyUploadUrl = init.ProxyRequired ? $"/api/profiles/me/avatar/{init.FileId}/content" : null,
            completeUrl = $"/api/profiles/me/avatar/{init.FileId}/complete",
            expiresAt = init.ExpiresAt,
        });
    }

    [HttpPut("me/avatar/{fileId:guid}/content")]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> ProxyAvatarUpload(Guid fileId, CancellationToken ct)
    {
        var contentType = Request.ContentType ?? "application/octet-stream";
        await storage.ProxyUploadAsync(fileId, Request.Body, contentType, ct);
        return NoContent();
    }

    [HttpPost("me/avatar/{fileId:guid}/complete")]
    public async Task<IActionResult> CompleteAvatarUpload(Guid fileId, [FromBody] CompleteAvatarUploadRequest req, CancellationToken ct)
    {
        var result = await storage.CompleteUploadAsync(fileId, req.ChecksumSha256, req.SizeBytes, ct);

        if (env.IsDevelopment())
            await storage.DevMarkReadyAsync(fileId, ct);

        var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == UserId, ct);
        if (profile is null)
        {
            profile = new UserProfile { UserId = UserId };
            db.UserProfiles.Add(profile);
        }
        profile.AvatarFileId = fileId;
        profile.AvatarStatus = env.IsDevelopment() ? "ready" : result.Status;
        await db.SaveChangesAsync(ct);

        return Ok(profile);
    }

    [HttpGet("me/avatar/content")]
    public async Task<IActionResult> GetAvatarContent(CancellationToken ct)
    {
        var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == UserId, ct);
        if (profile is null || !profile.AvatarFileId.HasValue) return NotFound();

        var stream = await storage.DownloadContentAsync(profile.AvatarFileId.Value, ct);
        return File(stream, "image/jpeg");
    }
}
