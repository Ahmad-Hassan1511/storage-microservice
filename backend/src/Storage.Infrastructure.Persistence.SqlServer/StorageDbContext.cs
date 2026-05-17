using DomainFile = Storage.Domain.Entities.File;
using Storage.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Storage.Infrastructure.Persistence.SqlServer;

public class StorageDbContext : DbContext
{
    public StorageDbContext(DbContextOptions<StorageDbContext> options) : base(options) { }

    public DbSet<DomainFile> Files => Set<DomainFile>();
    public DbSet<FileCategory> FileCategories => Set<FileCategory>();
    public DbSet<FileVersion> FileVersions => Set<FileVersion>();
    public DbSet<FilePermission> FilePermissions => Set<FilePermission>();
    public DbSet<FileTag> FileTags => Set<FileTag>();
    public DbSet<AuditEntry> AuditLog => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StorageDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
