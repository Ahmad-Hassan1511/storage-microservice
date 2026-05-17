using MassTransit;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Storage.Sdk;

namespace Profile;

public sealed record FileReadyIntegrationEvent(
    Guid FileId,
    Guid TenantId,
    string OwnerService,
    string? DownloadUrl,
    Guid CorrelationId,
    DateTime OccurredAt,
    string Source);

/// <summary>
/// When a file.ready event arrives for profile-service, download the original,
/// generate 256x256 and 64x64 thumbnails, upload them via Storage SDK, and
/// update the profile with the thumbnail URLs.
/// </summary>
public sealed class AvatarFileReadyHandler(
    ProfileDbContext db,
    IStorageClient storage) : IConsumer<FileReadyIntegrationEvent>
{
    public async Task Consume(ConsumeContext<FileReadyIntegrationEvent> context)
    {
        var evt = context.Message;
        if (!evt.OwnerService.Equals("profile-service", StringComparison.OrdinalIgnoreCase))
            return;

        var profile = await db.UserProfiles
            .FirstOrDefaultAsync(p => p.AvatarFileId == evt.FileId, context.CancellationToken);
        if (profile is null) return;

        // Download original
        var originalStream = await storage.DownloadContentAsync(evt.FileId, context.CancellationToken);
        using var image = await Image.LoadAsync(originalStream, context.CancellationToken);

        // Generate 256×256 thumbnail
        var thumb256Url = await UploadThumbnailAsync(image, 256, evt.TenantId, context.CancellationToken);
        // Generate 64×64 thumbnail
        var thumb64Url = await UploadThumbnailAsync(image, 64, evt.TenantId, context.CancellationToken);

        profile.AvatarStatus = "ready";
        profile.AvatarUrl256 = thumb256Url;
        profile.AvatarUrl64 = thumb64Url;
        await db.SaveChangesAsync(context.CancellationToken);
    }

    private async Task<string?> UploadThumbnailAsync(
        Image source, int size, Guid tenantId, CancellationToken ct)
    {
        using var clone = source.Clone(ctx => ctx.Resize(size, size));
        using var ms = new MemoryStream();
        await clone.SaveAsWebpAsync(ms, cancellationToken: ct);
        ms.Position = 0;

        var result = await storage.UploadAsync(ms, new UploadFileRequest(
            CategoryId: "avatar-thumbnail",
            OriginalFileName: $"avatar_{size}x{size}.webp",
            MimeType: "image/webp",
            SizeBytes: ms.Length,
            OwnerService: "profile-service"), ct);

        var file = await storage.GetFileAsync(result.FileId, ct);
        return file?.DownloadUrl;
    }
}
