using Storage.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Storage.Infrastructure.Persistence.SqlServer.Seeders;

public sealed class FileCategorySeeder
{
    private readonly StorageDbContext _db;
    public FileCategorySeeder(StorageDbContext db) => _db = db;

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await _db.FileCategories.AnyAsync(ct)) return;

        var now = DateTime.UtcNow;

        var categories = new[]
        {
            new FileCategory
            {
                Id = "image",
                DisplayName = "Image",
                MaxSizeBytes = 20 * 1024 * 1024,
                AllowedMimeTypes = ["image/jpeg", "image/png", "image/gif", "image/webp", "image/svg+xml"],
                AllowedExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg"],
                IsLargeFile = false,
                AllowedOwnerServices = [],
                SupportsPreview = true,
                AntivirusRequired = true,
                RequiresAiValidation = false
            },
            new FileCategory
            {
                Id = "document",
                DisplayName = "Document",
                MaxSizeBytes = 100 * 1024 * 1024,
                AllowedMimeTypes = ["application/pdf", "application/msword",
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document"],
                AllowedExtensions = [".pdf", ".doc", ".docx"],
                IsLargeFile = false,
                AllowedOwnerServices = [],
                SupportsPreview = true,
                AntivirusRequired = true,
                RequiresAiValidation = false
            },
            new FileCategory
            {
                Id = "video",
                DisplayName = "Video",
                MaxSizeBytes = 5L * 1024 * 1024 * 1024,
                AllowedMimeTypes = ["video/mp4", "video/webm", "video/quicktime"],
                AllowedExtensions = [".mp4", ".webm", ".mov"],
                IsLargeFile = true,
                MultipartThresholdBytes = 100 * 1024 * 1024,
                AllowedOwnerServices = [],
                SupportsPreview = false,
                AntivirusRequired = true,
                RequiresAiValidation = false
            },
            new FileCategory
            {
                Id = "audio",
                DisplayName = "Audio",
                MaxSizeBytes = 500 * 1024 * 1024,
                AllowedMimeTypes = ["audio/mpeg", "audio/wav", "audio/ogg"],
                AllowedExtensions = [".mp3", ".wav", ".ogg"],
                IsLargeFile = false,
                AllowedOwnerServices = [],
                SupportsPreview = false,
                AntivirusRequired = true,
                RequiresAiValidation = false
            },
            new FileCategory
            {
                Id = "archive",
                DisplayName = "Archive",
                MaxSizeBytes = 2L * 1024 * 1024 * 1024,
                AllowedMimeTypes = ["application/zip", "application/x-tar", "application/gzip"],
                AllowedExtensions = [".zip", ".tar", ".gz", ".tar.gz"],
                IsLargeFile = true,
                MultipartThresholdBytes = 100 * 1024 * 1024,
                AllowedOwnerServices = [],
                SupportsPreview = false,
                AntivirusRequired = true,
                RequiresAiValidation = false
            },
            new FileCategory
            {
                Id = "spreadsheet",
                DisplayName = "Spreadsheet",
                MaxSizeBytes = 50 * 1024 * 1024,
                AllowedMimeTypes = ["application/vnd.ms-excel",
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "text/csv"],
                AllowedExtensions = [".xls", ".xlsx", ".csv"],
                IsLargeFile = false,
                AllowedOwnerServices = [],
                SupportsPreview = true,
                AntivirusRequired = true,
                RequiresAiValidation = false
            },
            new FileCategory
            {
                Id = "presentation",
                DisplayName = "Presentation",
                MaxSizeBytes = 200 * 1024 * 1024,
                AllowedMimeTypes = ["application/vnd.ms-powerpoint",
                    "application/vnd.openxmlformats-officedocument.presentationml.presentation"],
                AllowedExtensions = [".ppt", ".pptx"],
                IsLargeFile = false,
                AllowedOwnerServices = [],
                SupportsPreview = true,
                AntivirusRequired = true,
                RequiresAiValidation = false
            }
        };

        _db.FileCategories.AddRange(categories);

        foreach (var category in categories)
        {
            var entry = _db.Entry(category);
            entry.Property("Visibility").CurrentValue = "private";
            entry.Property("LifecycleTier").CurrentValue = "hot";
            entry.Property("CreatedAt").CurrentValue = now;
            entry.Property("UpdatedAt").CurrentValue = now;
        }

        await _db.SaveChangesAsync(ct);
    }
}
