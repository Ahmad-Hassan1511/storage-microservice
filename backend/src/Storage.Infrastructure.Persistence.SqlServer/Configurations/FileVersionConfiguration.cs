using Storage.Domain.Entities;
using Storage.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Storage.Infrastructure.Persistence.SqlServer.Configurations;

public class FileVersionConfiguration : IEntityTypeConfiguration<FileVersion>
{
    public void Configure(EntityTypeBuilder<FileVersion> b)
    {
        b.HasKey(v => new { v.FileId, v.VersionNumber });
        b.Ignore(v => v.Id);
        b.Ignore(v => v.DomainEvents);

        b.Property(v => v.StorageKey)
            .HasConversion(sk => sk.Value, s => new StorageKey(s))
            .HasMaxLength(512)
            .IsRequired();

        b.Property(v => v.Checksum)
            .HasConversion(c => c.Value, s => new Checksum(s))
            .HasMaxLength(128)
            .IsRequired();

        b.Property(v => v.SizeBytes).IsRequired();
        b.Property(v => v.CreatedAt).IsRequired();

        b.HasOne<Storage.Domain.Entities.File>()
            .WithMany()
            .HasForeignKey(v => v.FileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
