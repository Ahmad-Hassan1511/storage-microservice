using Storage.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Storage.Infrastructure.Persistence.SqlServer.Configurations;

public class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> b)
    {
        b.Property<long>("AuditLogId").ValueGeneratedOnAdd();
        b.HasKey("AuditLogId");
        b.Ignore(a => a.Id);
        b.Ignore(a => a.DomainEvents);

        b.Property(a => a.FileId).IsRequired();
        b.Property(a => a.Action).HasMaxLength(128).IsRequired();

        b.Property(a => a.PerformedBy)
            .HasColumnName("Actor")
            .HasMaxLength(256)
            .IsRequired();

        b.Property(a => a.PerformedAt)
            .HasColumnName("OccurredAt")
            .IsRequired();

        b.Property(a => a.Details)
            .HasColumnName("Metadata")
            .HasColumnType("nvarchar(max)");

        b.Property<string?>("Ip").HasColumnType("varchar(64)");
        b.Property<string?>("UserAgent").HasMaxLength(512);

        b.HasOne<Storage.Domain.Entities.File>()
            .WithMany()
            .HasForeignKey(a => a.FileId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
