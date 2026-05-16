using Storage.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace Storage.Infrastructure.Persistence.SqlServer.Configurations;

public class FileCategoryConfiguration : IEntityTypeConfiguration<FileCategory>
{
    private static readonly JsonSerializerOptions _json = new();

    public void Configure(EntityTypeBuilder<FileCategory> b)
    {
        b.HasKey(c => c.Id);
        b.Property(c => c.Id).HasMaxLength(64);
        b.Property(c => c.DisplayName).HasMaxLength(256).IsRequired();
        b.Property(c => c.MaxSizeBytes).IsRequired();

        b.Property(c => c.AllowedMimeTypes)
            .HasConversion(
                v => JsonSerializer.Serialize(v, _json),
                v => JsonSerializer.Deserialize<string[]>(v, _json)!)
            .HasColumnType("nvarchar(max)");

        b.Property(c => c.AllowedExtensions)
            .HasConversion(
                v => JsonSerializer.Serialize(v, _json),
                v => JsonSerializer.Deserialize<string[]>(v, _json)!)
            .HasColumnType("nvarchar(max)");

        b.Property(c => c.AllowedOwnerServices)
            .HasConversion(
                v => JsonSerializer.Serialize(v, _json),
                v => JsonSerializer.Deserialize<string[]>(v, _json)!)
            .HasColumnType("nvarchar(max)");

        b.Property(c => c.IsLargeFile);
        b.Property(c => c.MultipartThresholdBytes);
        b.Property(c => c.SupportsPreview);
        b.Property(c => c.AntivirusRequired);
        b.Property(c => c.RequiresAiValidation);
        b.Property(c => c.AiValidationStrategy).HasMaxLength(128);

        b.Property<string>("Visibility").HasMaxLength(16).IsRequired();
        b.Property<string?>("PreviewStrategy").HasMaxLength(64);
        b.Property<string?>("ThumbnailSizes").HasColumnType("nvarchar(max)");
        b.Property<int?>("RetentionDays");
        b.Property<string>("LifecycleTier").HasMaxLength(32).IsRequired();
        b.Property<DateTime>("CreatedAt").IsRequired();
        b.Property<DateTime>("UpdatedAt").IsRequired();
    }
}
