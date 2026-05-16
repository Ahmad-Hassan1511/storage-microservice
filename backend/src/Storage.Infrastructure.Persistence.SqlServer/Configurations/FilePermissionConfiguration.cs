using Storage.Domain.Entities;
using Storage.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Storage.Infrastructure.Persistence.SqlServer.Configurations;

public class FilePermissionConfiguration : IEntityTypeConfiguration<FilePermission>
{
    public void Configure(EntityTypeBuilder<FilePermission> b)
    {
        b.HasKey(p => new { p.FileId, p.PrincipalType, p.PrincipalId, p.Permission });
        b.Ignore(p => p.Id);
        b.Ignore(p => p.DomainEvents);

        b.Property(p => p.PrincipalType).HasMaxLength(16).IsRequired();
        b.Property(p => p.PrincipalId).HasMaxLength(256).IsRequired();

        b.Property(p => p.Permission)
            .HasConversion(
                perm => perm.ToString().ToLowerInvariant(),
                s => Enum.Parse<Permission>(s, ignoreCase: true))
            .HasMaxLength(16)
            .IsRequired();

        b.ToTable("FilePermissions", t =>
        {
            t.HasCheckConstraint("CK_FilePermissions_PrincipalType",
                "PrincipalType IN ('service','user')");
        });

        b.HasOne<Storage.Domain.Entities.File>()
            .WithMany()
            .HasForeignKey(p => p.FileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
