using Storage.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Storage.Infrastructure.Persistence.SqlServer.Configurations;

public class FileTagConfiguration : IEntityTypeConfiguration<FileTag>
{
    public void Configure(EntityTypeBuilder<FileTag> b)
    {
        b.HasKey(t => new { t.FileId, t.Key });
        b.Ignore(t => t.Id);
        b.Ignore(t => t.DomainEvents);

        b.Property(t => t.Key).HasMaxLength(128).IsRequired();
        b.Property(t => t.Value).HasMaxLength(512).IsRequired();

        b.HasOne<Storage.Domain.Entities.File>()
            .WithMany()
            .HasForeignKey(t => t.FileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
