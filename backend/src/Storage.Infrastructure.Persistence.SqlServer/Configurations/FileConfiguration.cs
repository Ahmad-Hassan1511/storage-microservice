using DomainFile = Storage.Domain.Entities.File;
using Storage.Domain.Entities;
using Storage.Domain.Enums;
using Storage.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Storage.Infrastructure.Persistence.SqlServer.Configurations;

public class FileConfiguration : IEntityTypeConfiguration<DomainFile>
{
    public void Configure(EntityTypeBuilder<DomainFile> b)
    {
        b.HasKey(f => f.Id);

        b.ToTable("Files", t =>
        {
            t.HasCheckConstraint("CK_Files_Status",
                "Status IN ('pending','scanning','ready','quarantined','deleted')");
        });

        b.Property(f => f.TenantId).IsRequired();
        b.Property(f => f.OwnerService).HasMaxLength(128).IsRequired();
        b.Property(f => f.CategoryId).HasMaxLength(64).IsRequired();

        b.Property(f => f.OriginalFileName)
            .HasColumnName("OriginalName")
            .HasMaxLength(512)
            .IsRequired();

        b.Property(f => f.MimeType).HasMaxLength(256).IsRequired();
        b.Property(f => f.SizeBytes).IsRequired();

        b.Property(f => f.Status)
            .HasConversion(
                s => s.ToString().ToLowerInvariant(),
                s => Enum.Parse<FileStatus>(s, ignoreCase: true))
            .HasMaxLength(32)
            .IsRequired();

        b.Property(f => f.Visibility)
            .HasConversion(
                v => v.ToString().ToLowerInvariant(),
                v => Enum.Parse<Visibility>(v, ignoreCase: true))
            .HasMaxLength(16)
            .IsRequired();

        b.Property(f => f.StorageKey)
            .HasConversion(
                sk => sk == null ? (string?)null : sk.Value,
                s => s == null ? null : new StorageKey(s))
            .HasMaxLength(512);

        b.Property(f => f.Checksum)
            .HasConversion(
                c => c == null ? (string?)null : c.Value,
                s => s == null ? null : new Checksum(s))
            .HasMaxLength(128);

        b.Property(f => f.CreatedAt).IsRequired();
        b.Property(f => f.UpdatedAt).IsRequired();
        b.Property(f => f.PreviewFileId);
        b.Property(f => f.ThumbnailFileId);

        b.Property<DateTime?>("DeletedAt");

        b.HasQueryFilter(f => EF.Property<DateTime?>(f, "DeletedAt") == null);

        b.HasOne<DomainFile>().WithMany()
            .HasForeignKey(f => f.PreviewFileId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        b.HasOne<DomainFile>().WithMany()
            .HasForeignKey(f => f.ThumbnailFileId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        b.HasIndex(f => f.TenantId);
        b.HasIndex(f => new { f.TenantId, f.OwnerService });
        b.HasIndex(f => new { f.TenantId, f.CategoryId });

        b.Property<byte[]>("RowVersion").IsRowVersion();

        b.Ignore(f => f.DomainEvents);
    }
}
