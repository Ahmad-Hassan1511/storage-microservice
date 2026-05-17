# Phase 3: Persistence Adapter - Research

**Researched:** 2026-05-16
**Domain:** EF Core 10 / SQL Server / Repository Pattern / Migrations / Testcontainers
**Confidence:** HIGH (official NuGet versions confirmed; architecture contracts read from source)

---

## Summary

Phase 3 implements `Storage.Infrastructure.Persistence.SqlServer` — the EF Core adapter that fulfils
the five persistence port interfaces (`IUnitOfWork`, `IFileRepository`, `IFileCategoryRepository`,
`IFileVersionRepository`, `IPermissionRepository`, `IAuditRepository`) defined in Phase 2.

The central challenge of this phase is **domain-schema bridging**: Phase 1 domain entities are frozen
(committed) and several columns required by the §6.3 schema do not exist on those entities. All gaps
are closed with EF Core **shadow properties**, **value converters**, and a **SaveChanges interceptor**.
No domain entity is modified.

A second important concern is the interaction between `EnableRetryOnFailure` (wired in §10.6) and
user-initiated transactions: wrapping `BeginTransactionAsync` inside
`Database.CreateExecutionStrategy().ExecuteAsync(...)` is mandatory, or EF Core throws
`InvalidOperationException` at runtime.

**Primary recommendation:** Use `IEntityTypeConfiguration<T>` per entity class (one file each),
shadow properties for columns absent from the domain, `HasConversion` (not `OwnsOne`) for the two
single-property value objects (`StorageKey`, `Checksum`), an explicit `IsRowVersion()` shadow
property for optimistic concurrency, a `SaveChangesInterceptor` that stamps `DeletedAt` and
updates `UpdatedAt`, and a runtime `IDbSeeder` for `FileCategory` seed rows (not `HasData` — shadow
property seeding via `HasData` anonymous-type syntax is fragile with 7+ shadow properties).

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| PERSIST-01 | `StorageDbContext` with DbSets for Files, FileCategories, FileVersions, FilePermissions, FileTags, AuditLog | StorageDbContext skeleton + `ApplyConfigurationsFromAssembly` pattern from §10.6 |
| PERSIST-02 | EF Fluent API configs matching §6.3 schema including all indexes and check constraints | Domain-schema bridge table; `HasIndex`, `ToTable(t => t.HasCheckConstraint(...))`, shadow property patterns |
| PERSIST-03 | Initial migration creates full schema; seeder inserts starter FileCategory rows | `IDesignTimeDbContextFactory`, `IDbSeeder` runtime seeder (idempotent via `AnyAsync` check) |
| PERSIST-04 | `EfUnitOfWork` implements `IUnitOfWork` with `ExecuteInTransactionAsync` | `CreateExecutionStrategy().ExecuteAsync` wrapping pattern (mandatory with EnableRetryOnFailure) |
| PERSIST-05 | `FileRepository` with soft-delete query filter and all query methods needed by use cases | `HasQueryFilter(f => f.Status != FileStatus.Deleted)`, cursor-based pagination, GetByIdAsync eager-load |
</phase_requirements>

---

## Domain-Schema Bridge Strategy

This is the load-bearing section. Phase 1 domain entities are frozen. Every gap is resolved by EF
configuration alone — no entity code changes.

| §6.3 Column / Issue | Domain Status | Bridge Strategy |
|---------------------|---------------|-----------------|
| `Files.OriginalName` (NVARCHAR 512) | Domain field named `OriginalFileName` | `HasColumnName("OriginalName")` in FileConfiguration |
| `Files.ChecksumSha256` (CHAR 64) | Domain has `Checksum?` value object | `HasConversion(c => c.Value, v => new Checksum(v))` on nullable property |
| `Files.StorageKey` (VARCHAR 1024) | Domain has `StorageKey?` value object | `HasConversion(sk => sk.Value, v => new StorageKey(v))` on nullable property |
| `Files.StorageBucket` (VARCHAR 128) | Not on domain entity | Shadow property `"StorageBucket"` — default empty string; Phase 4 storage adapter updates it via `Entry(file).Property("StorageBucket")` |
| `Files.OwnerUserId` (UNIQUEIDENTIFIER NULL) | Not on domain entity | Shadow property `"OwnerUserId"` of `Guid?` |
| `Files.CurrentVersion` (INT DEFAULT 1) | Not on domain entity | Shadow property `"CurrentVersion"` of `int`, default value 1 |
| `Files.DeletedAt` (DATETIME2 NULL) | Not on domain entity | Shadow property `"DeletedAt"` of `DateTime?`; set by `SoftDeleteInterceptor` when `Status` transitions to `Deleted` |
| `Files.RowVersion` | Not in §6.3 SQL; required by §15.4 `DbUpdateConcurrencyException` test | Shadow `byte[]` property, `.IsRowVersion()` — **augments §6.3 to satisfy §15.4** — EF adds `ROWVERSION` column to migration |
| `FilePermissions` composite PK | Domain inherits `EntityBase.Id` (Guid) | `HasKey(p => new { p.FileId, p.PrincipalType, p.PrincipalId, p.Permission })` then `Ignore(p => p.Id)` |
| `FileTags` composite PK | Domain inherits `EntityBase.Id` (Guid) | `HasKey(t => new { t.FileId, t.Key })` then `Ignore(t => t.Id)` |
| `AuditLog.Id BIGINT IDENTITY` | Domain `EntityBase.Id` is Guid | Shadow `long "AuditLogId"` as actual PK. Domain `Id` ignored via `Ignore(a => a.Id)`. `ValueGeneratedOnAdd()` on shadow long key |
| `AuditLog.Actor` | Domain has `PerformedBy` | `HasColumnName("Actor")` |
| `AuditLog.OccurredAt` | Domain has `PerformedAt` | `HasColumnName("OccurredAt")` |
| `AuditLog.Metadata` | Domain has `Details` | `HasColumnName("Metadata")` |
| `AuditLog.Ip`, `AuditLog.UserAgent` | Not on domain entity | Shadow properties `"Ip"` (VARCHAR 64) and `"UserAgent"` (NVARCHAR 512) |
| `FileCategory.AllowedMimeTypes` (NVARCHAR 2000 JSON) | Domain is `string[]` | `HasConversion(arr => JsonSerializer.Serialize(arr, null), json => JsonSerializer.Deserialize<string[]>(json, null) ?? [])` with `HasColumnType("nvarchar(2000)")` |
| `FileCategory.AllowedExtensions` (NVARCHAR 2000 JSON) | Domain is `string[]` | Same JSON converter pattern |
| `FileCategory.AllowedOwnerServices` (NVARCHAR 1000 JSON) | Domain is `string[]` | Same JSON converter pattern |
| `FileCategory.Visibility`, `PreviewStrategy`, `ThumbnailSizes`, `RetentionDays`, `LifecycleTier`, `CreatedAt`, `UpdatedAt` | Not on domain entity | Shadow properties; populated by `IDbSeeder` runtime seeder (not `HasData` — too many shadow props for anonymous-type syntax) |
| `FileVersion` composite PK | Domain inherits `EntityBase.Id` (Guid) | `HasKey(v => new { v.FileId, v.VersionNumber })` then `Ignore(v => v.Id)` |
| `FileVersion.StorageKey` | Domain has `StorageKey` value object (required) | `HasConversion(sk => sk.Value, v => new StorageKey(v))` |
| `FileVersion.Checksum` | Domain has `Checksum` value object (required) | `HasConversion(c => c.Value, v => new Checksum(v))` |
| `Files.Status` (VARCHAR 32) | Domain is `FileStatus` enum | `HasConversion` to/from lowercase string — matches CHECK constraint values |
| `Files.Visibility` (VARCHAR 16) | Domain is `Visibility` enum | `HasConversion` to/from lowercase string |
| `FilePermissions.Permission` (VARCHAR 16) | Domain is `Permission` enum | `HasConversion` to/from lowercase string |
| `FileCategories` not in §10.6 DbContext example | Required by PERSIST-01 | Add `public DbSet<FileCategory> FileCategories => Set<FileCategory>();` explicitly |
| `EntityBase.DomainEvents` collection | All entities inherit `IReadOnlyList<DomainEvent> DomainEvents` | `b.Ignore(e => e.DomainEvents)` in EVERY entity configuration — EF convention scanner will attempt to map `DomainEvent` as an entity and throw at model building without this ignore |

**Soft-delete dual-signal decision:** §6.3 has `DeletedAt` column; CLAUDE.md says `status=deleted` is
the canonical deleted signal. Use `Status != FileStatus.Deleted` as the global query filter predicate.
The `SoftDeleteInterceptor` additionally stamps `DeletedAt` shadow property when it detects a status
transition to `Deleted`. Both signals are present in the database; the query filter uses only `Status`.

---

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `Microsoft.EntityFrameworkCore.SqlServer` | 10.0.8 | SQL Server EF Core provider | Matches .NET 10 TFM; GA as of Nov 2025; 10.0.8 current as of May 2026 |
| `Microsoft.EntityFrameworkCore.Design` | 10.0.8 | Design-time tooling (migrations) | Required for `dotnet ef migrations add` in the persistence project |
| `Testcontainers.MsSql` | 4.11.0 | SQL Server Docker container for integration tests | Official Testcontainers .NET module; uses `mcr.microsoft.com/mssql/server:2022-latest` |

> **Note (10.0.6+ change):** `Microsoft.EntityFrameworkCore.Tools` no longer pulls
> `Microsoft.EntityFrameworkCore.Design` as a transitive dependency starting from 10.0.6.
> Add `Microsoft.EntityFrameworkCore.Design` as an explicit `<PackageReference>` to the
> persistence project with `<PrivateAssets>all</PrivateAssets>`.

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `Microsoft.EntityFrameworkCore.Tools` | 10.0.8 | `dotnet ef` CLI scaffolding | Development only — `PrivateAssets=all` |
| `xunit.v3` | 3.2.* | Test framework | Integration test project |
| `FluentAssertions` | 8.9.* | Assertion library | Matches Phase 2 test conventions |
| `Microsoft.NET.Test.Sdk` | 17.14.1 | Test SDK | Matches Phase 2 test conventions |

### Installation

```xml
<!-- Storage.Infrastructure.Persistence.SqlServer.csproj additions -->
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.8" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.8">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
</PackageReference>

<!-- Storage.Infrastructure.Persistence.SqlServer.Tests.csproj -->
<PackageReference Include="Testcontainers.MsSql" Version="4.11.0" />
```

---

## Architecture Patterns

### Recommended Project Structure

```
Storage.Infrastructure.Persistence.SqlServer/
├── StorageDbContext.cs                     # DbContext, ApplyConfigurationsFromAssembly
├── StorageDbContextFactory.cs              # IDesignTimeDbContextFactory for dotnet ef
├── EfUnitOfWork.cs                         # IUnitOfWork implementation
├── Interceptors/
│   └── SoftDeleteInterceptor.cs            # SaveChanges interceptor - stamps DeletedAt, UpdatedAt
├── Configurations/
│   ├── FileConfiguration.cs                # IEntityTypeConfiguration<File>
│   ├── FileCategoryConfiguration.cs        # IEntityTypeConfiguration<FileCategory>
│   ├── FileVersionConfiguration.cs
│   ├── FilePermissionConfiguration.cs
│   ├── FileTagConfiguration.cs
│   └── AuditEntryConfiguration.cs
├── Seeders/
│   └── FileCategorySeeder.cs               # IDbSeeder — idempotent runtime seed
├── Repositories/
│   ├── FileRepository.cs                   # IFileRepository
│   ├── FileCategoryRepository.cs           # IFileCategoryRepository
│   ├── FileVersionRepository.cs            # IFileVersionRepository
│   ├── PermissionRepository.cs             # IPermissionRepository
│   └── AuditRepository.cs                  # IAuditRepository
├── Migrations/
│   └── (generated by dotnet ef)
└── ServiceCollectionExtensions.cs          # AddPersistence(IServiceCollection, IConfiguration)
```

```
Storage.Infrastructure.Persistence.SqlServer.Tests/
├── Storage.Infrastructure.Persistence.SqlServer.Tests.csproj
├── SqlServerFixture.cs                     # Testcontainers MsSqlContainer, runs migrations + seeder
├── MigrationTests.cs
├── FileRepositoryTests.cs
├── TransactionTests.cs
└── SoftDeleteTests.cs
```

### Pattern 1: DbContext with ApplyConfigurationsFromAssembly

```csharp
// Source: §10.6 of storage-microservice-architecture.md + EF Core docs
public class StorageDbContext : DbContext
{
    public DbSet<DomainFile>     Files          => Set<DomainFile>();
    public DbSet<FileCategory>   FileCategories => Set<FileCategory>();
    public DbSet<FileVersion>    FileVersions   => Set<FileVersion>();
    public DbSet<FilePermission> Permissions    => Set<FilePermission>();
    public DbSet<FileTag>        Tags           => Set<FileTag>();
    public DbSet<AuditEntry>     Audit          => Set<AuditEntry>();

    public StorageDbContext(DbContextOptions<StorageDbContext> opt) : base(opt) { }

    protected override void OnModelCreating(ModelBuilder mb)
        => mb.ApplyConfigurationsFromAssembly(typeof(StorageDbContext).Assembly);
}
```

### Pattern 2: IEntityTypeConfiguration per Entity (File example)

```csharp
// Source: EF Core Fluent API documentation
internal sealed class FileConfiguration : IEntityTypeConfiguration<DomainFile>
{
    public void Configure(EntityTypeBuilder<DomainFile> b)
    {
        b.ToTable("Files", t =>
        {
            // CHECK constraint — use ToTable overload (HasCheckConstraint direct on builder is obsolete in EF 10)
            t.HasCheckConstraint("CK_Files_Status",
                "[Status] IN ('pending','scanning','ready','quarantined','deleted')");
        });

        b.HasKey(f => f.Id);

        // Ignore EntityBase.DomainEvents — EF would otherwise try to map DomainEvent as an entity
        b.Ignore(f => f.DomainEvents);

        // Column renames
        b.Property(f => f.OriginalFileName).HasColumnName("OriginalName").HasMaxLength(512).IsRequired();
        b.Property(f => f.MimeType).HasMaxLength(128).IsRequired();
        b.Property(f => f.OwnerService).HasMaxLength(64).IsRequired();
        b.Property(f => f.TenantId).IsRequired();
        b.Property(f => f.SizeBytes).IsRequired();

        // Value objects via HasConversion (not OwnsOne — avoids null-tracking pitfalls)
        b.Property(f => f.StorageKey)
            .HasColumnName("StorageKey")
            .HasMaxLength(1024)
            .HasConversion(sk => sk!.Value, v => new StorageKey(v))
            .IsRequired(false);

        b.Property(f => f.Checksum)
            .HasColumnName("ChecksumSha256")
            .HasColumnType("char(64)")
            .HasConversion(c => c!.Value, v => new Checksum(v))
            .IsRequired(false);

        // Enum to lowercase string
        b.Property(f => f.Status)
            .HasMaxLength(32)
            .HasConversion(
                s => s.ToString().ToLowerInvariant(),
                v => Enum.Parse<FileStatus>(v, ignoreCase: true));

        b.Property(f => f.Visibility)
            .HasMaxLength(16)
            .HasConversion(
                v => v.ToString().ToLowerInvariant(),
                v => Enum.Parse<Visibility>(v, ignoreCase: true));

        // Shadow properties
        b.Property<string>("StorageBucket").HasMaxLength(128).HasDefaultValue("").IsRequired();
        b.Property<Guid?>("OwnerUserId");
        b.Property<int>("CurrentVersion").HasDefaultValue(1).IsRequired();
        b.Property<DateTime?>("DeletedAt");
        b.Property<byte[]>("RowVersion").IsRowVersion();  // optimistic concurrency — augments §6.3

        // Self-references
        b.HasOne<DomainFile>().WithMany().HasForeignKey(f => f.PreviewFileId).IsRequired(false)
            .OnDelete(DeleteBehavior.NoAction);
        b.HasOne<DomainFile>().WithMany().HasForeignKey(f => f.ThumbnailFileId).IsRequired(false)
            .OnDelete(DeleteBehavior.NoAction);

        // Indexes from §6.3
        b.HasIndex(f => new { f.TenantId, f.OwnerService }).HasDatabaseName("IX_Files_Tenant_Owner");
        b.HasIndex(f => new { f.TenantId, f.Status })     .HasDatabaseName("IX_Files_Tenant_Status");
        b.HasIndex(f => new { f.TenantId, f.CategoryId }) .HasDatabaseName("IX_Files_Tenant_Category");
        b.HasIndex("ChecksumSha256")                       .HasDatabaseName("IX_Files_Checksum");

        // Global soft-delete query filter
        b.HasQueryFilter(f => f.Status != FileStatus.Deleted);

        // FK to FileCategory
        b.HasOne<FileCategory>().WithMany().HasForeignKey(f => f.CategoryId)
            .HasConstraintName("FK_Files_Category");

        // Collections
        b.HasMany(f => f.Versions).WithOne().HasForeignKey(v => v.FileId);
        b.HasMany(f => f.Permissions).WithOne().HasForeignKey(p => p.FileId);
        b.HasMany(f => f.Tags).WithOne().HasForeignKey(t => t.FileId);
        b.HasMany(f => f.AuditEntries).WithOne().HasForeignKey(a => a.FileId);
    }
}
```

### Pattern 3: Composite PK for owned collections (FilePermission example)

```csharp
internal sealed class FilePermissionConfiguration : IEntityTypeConfiguration<FilePermission>
{
    public void Configure(EntityTypeBuilder<FilePermission> b)
    {
        b.ToTable("FilePermissions");

        // Ignore inherited EntityBase members
        b.Ignore(p => p.Id);
        b.Ignore(p => p.DomainEvents);

        b.HasKey(p => new { p.FileId, p.PrincipalType, p.PrincipalId, p.Permission });

        b.Property(p => p.PrincipalType).HasMaxLength(16).IsRequired();
        b.Property(p => p.PrincipalId).HasMaxLength(128).IsRequired();
        b.Property(p => p.Permission)
            .HasMaxLength(16)
            .HasConversion(
                perm => perm.ToString().ToLowerInvariant(),
                v => Enum.Parse<Permission>(v, ignoreCase: true));
    }
}
```

### Pattern 4: AuditLog with shadow BIGINT PK

```csharp
internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> b)
    {
        b.ToTable("AuditLog");

        // Schema uses BIGINT IDENTITY — domain has Guid Id
        b.Ignore(a => a.Id);
        b.Ignore(a => a.DomainEvents);
        b.Property<long>("AuditLogId").ValueGeneratedOnAdd();
        b.HasKey("AuditLogId");

        // Column renames
        b.Property(a => a.PerformedBy).HasColumnName("Actor").HasMaxLength(256).IsRequired();
        b.Property(a => a.PerformedAt).HasColumnName("OccurredAt").IsRequired();
        b.Property(a => a.Details).HasColumnName("Metadata").HasColumnType("nvarchar(max)");

        // Shadow properties for columns not in domain
        b.Property<string?>("Ip").HasMaxLength(64);
        b.Property<string?>("UserAgent").HasMaxLength(512);

        b.HasIndex("FileId", "OccurredAt").IsDescending(false, true)
            .HasDatabaseName("IX_AuditLog_FileId_OccurredAt");
    }
}
```

### Pattern 5: FileCategory with JSON arrays and runtime IDbSeeder

`FileCategory` does NOT inherit `EntityBase` — it has no `DomainEvents` to ignore.

```csharp
internal sealed class FileCategoryConfiguration : IEntityTypeConfiguration<FileCategory>
{
    public void Configure(EntityTypeBuilder<FileCategory> b)
    {
        b.ToTable("FileCategories");
        b.HasKey(c => c.Id);
        b.Property(c => c.Id).HasMaxLength(64);
        b.Property(c => c.DisplayName).HasMaxLength(256).IsRequired();
        b.Property(c => c.MaxSizeBytes).IsRequired();

        // JSON array columns — explicit converter (EF 10 native JSON requires SQL Server 2025 compat level)
        b.Property(c => c.AllowedMimeTypes)
            .HasColumnType("nvarchar(2000)")
            .HasConversion(
                arr => JsonSerializer.Serialize(arr, (JsonSerializerOptions?)null),
                json => JsonSerializer.Deserialize<string[]>(json, (JsonSerializerOptions?)null) ?? []);

        b.Property(c => c.AllowedExtensions)
            .HasColumnType("nvarchar(2000)")
            .HasConversion(
                arr => JsonSerializer.Serialize(arr, (JsonSerializerOptions?)null),
                json => JsonSerializer.Deserialize<string[]>(json, (JsonSerializerOptions?)null) ?? []);

        b.Property(c => c.AllowedOwnerServices)
            .HasColumnType("nvarchar(1000)")
            .HasConversion(
                arr => JsonSerializer.Serialize(arr, (JsonSerializerOptions?)null),
                json => JsonSerializer.Deserialize<string[]>(json, (JsonSerializerOptions?)null) ?? []);

        // Shadow properties for schema columns absent from domain
        b.Property<string>("Visibility").HasMaxLength(16).HasDefaultValue("private").IsRequired();
        b.Property<string?>("PreviewStrategy").HasMaxLength(64);
        b.Property<string?>("ThumbnailSizes").HasMaxLength(256);
        b.Property<int?>("RetentionDays");
        b.Property<string>("LifecycleTier").HasMaxLength(16).HasDefaultValue("hot").IsRequired();
        b.Property<DateTime>("CreatedAt").IsRequired();
        b.Property<DateTime>("UpdatedAt").IsRequired();

        // DO NOT use HasData() here — 7+ shadow properties make anonymous-type seeding brittle.
        // Use FileCategorySeeder (IDbSeeder) called from AddPersistence after MigrateAsync.
    }
}
```

**IDbSeeder pattern** (idempotent runtime seed — called from `AddPersistence` after migration):

```csharp
public sealed class FileCategorySeeder
{
    private readonly StorageDbContext _db;
    public FileCategorySeeder(StorageDbContext db) => _db = db;

    public async Task SeedAsync(CancellationToken ct)
    {
        if (await _db.FileCategories.AnyAsync(ct)) return;  // idempotency guard

        var now = DateTime.UtcNow;
        _db.FileCategories.AddRange(
            new FileCategory
            {
                Id = "avatar", DisplayName = "Avatar",
                MaxSizeBytes = 5 * 1024 * 1024,
                AllowedMimeTypes = ["image/jpeg", "image/png", "image/webp"],
                AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"],
                IsLargeFile = false, SupportsPreview = true, AntivirusRequired = true,
                AllowedOwnerServices = []
            }
            // ... remaining 6 categories from §6.1
        );

        // Set shadow properties via ChangeTracker
        foreach (var entry in _db.ChangeTracker.Entries<FileCategory>())
        {
            entry.Property("Visibility").CurrentValue = "private";
            entry.Property("LifecycleTier").CurrentValue = "hot";
            entry.Property("CreatedAt").CurrentValue = now;
            entry.Property("UpdatedAt").CurrentValue = now;
        }

        await _db.SaveChangesAsync(ct);
    }
}
```

### Pattern 6: EfUnitOfWork with CreateExecutionStrategy

**Critical:** `EnableRetryOnFailure` and user-initiated transactions are incompatible unless wrapped
in `CreateExecutionStrategy().ExecuteAsync(...)`. Failure to do this causes `InvalidOperationException`
at runtime the first time a retried transaction is attempted.

```csharp
// Source: https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency
public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly StorageDbContext _db;

    public EfUnitOfWork(
        StorageDbContext db,
        IFileRepository files,
        IFileCategoryRepository categories,
        IFileVersionRepository fileVersions,
        IPermissionRepository permissions,
        IAuditRepository audit)
    {
        _db = db;
        Files = files;
        Categories = categories;
        FileVersions = fileVersions;
        Permissions = permissions;
        Audit = audit;
    }

    public IFileRepository Files { get; }
    public IFileCategoryRepository Categories { get; }
    public IFileVersionRepository FileVersions { get; }
    public IPermissionRepository Permissions { get; }
    public IAuditRepository Audit { get; }

    public Task<int> SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);

    public Task ExecuteInTransactionAsync(Func<Task> work, CancellationToken ct)
    {
        // MUST use CreateExecutionStrategy — EnableRetryOnFailure forbids bare user transactions
        var strategy = _db.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            await work();
            await tx.CommitAsync(ct);
        });
    }
}
```

### Pattern 7: SoftDeleteInterceptor

```csharp
public sealed class SoftDeleteInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData data, InterceptionResult<int> result)
    {
        StampSoftDelete(data.Context);
        return base.SavingChanges(data, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
    {
        StampSoftDelete(data.Context);
        return base.SavingChangesAsync(data, result, ct);
    }

    private static void StampSoftDelete(DbContext? ctx)
    {
        if (ctx is null) return;
        var now = DateTime.UtcNow;
        foreach (var entry in ctx.ChangeTracker.Entries<DomainFile>())
        {
            if (entry.State == EntityState.Modified &&
                entry.Entity.Status == FileStatus.Deleted &&
                entry.Property<DateTime?>("DeletedAt").CurrentValue is null)
            {
                entry.Property<DateTime?>("DeletedAt").CurrentValue = now;
            }
            if (entry.State != EntityState.Deleted)
                entry.Property(e => e.UpdatedAt).CurrentValue = now;
        }
    }
}
```

### Pattern 8: Design-Time Factory

```csharp
// StorageDbContextFactory.cs — must be in same project as StorageDbContext
public sealed class StorageDbContextFactory : IDesignTimeDbContextFactory<StorageDbContext>
{
    public StorageDbContext CreateDbContext(string[] args)
    {
        var optBuilder = new DbContextOptionsBuilder<StorageDbContext>();
        var connStr = Environment.GetEnvironmentVariable("ConnectionStrings__Database")
            ?? "Server=localhost,1433;Database=StorageDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True";
        optBuilder.UseSqlServer(connStr,
            sql => sql.MigrationsAssembly("Storage.Infrastructure.Persistence.SqlServer"));
        return new StorageDbContext(optBuilder.Options);
    }
}
```

### Pattern 9: Cursor-based Pagination in FileRepository

`FileListQuery` has `Cursor` (opaque string), `PageSize`, `TenantId`, optional `OwnerService`,
`CategoryId`, `Tags` dictionary. The cursor encodes the `CreatedAt` + `Id` of the last seen row.

```csharp
// Cursor = base64(createdAt_ticks:id_string) — opaque to callers
public async Task<IReadOnlyList<DomainFile>> ListAsync(
    FileListQuery query, CancellationToken ct)
{
    var q = _db.Files
        .Include(f => f.Permissions)
        .Where(f => f.TenantId == query.TenantId);

    if (query.OwnerService is not null)
        q = q.Where(f => f.OwnerService == query.OwnerService);
    if (query.CategoryId is not null)
        q = q.Where(f => f.CategoryId == query.CategoryId);
    if (query.Tags is { Count: > 0 })
    {
        foreach (var (key, value) in query.Tags)
            q = q.Where(f => f.Tags.Any(t => t.Key == key && t.Value == value));
    }

    if (query.Cursor is not null)
    {
        var (cursorDate, cursorId) = DecodeCursor(query.Cursor);
        q = q.Where(f => f.CreatedAt < cursorDate
                      || (f.CreatedAt == cursorDate && f.Id.CompareTo(cursorId) < 0));
    }

    return await q
        .OrderByDescending(f => f.CreatedAt).ThenByDescending(f => f.Id)
        .Take(query.PageSize)
        .ToListAsync(ct);
}
```

### Anti-Patterns to Avoid

- **`OwnsOne` for single-property VOs:** Causes null-tracking issues and table-splitting complications.
  Use `HasConversion` for `StorageKey` and `Checksum`.
- **EF Core 10 native JSON type for `string[]`:** EF 10 maps primitives to native JSON only on
  SQL Server 2025 (compatibility level 170). Use `HasConversion` with `JsonSerializer` for
  `AllowedMimeTypes` etc., which works on SQL Server 2022 (Docker container in demo stack).
- **Bare `BeginTransactionAsync` with `EnableRetryOnFailure`:** Causes `InvalidOperationException`.
  Always wrap in `CreateExecutionStrategy().ExecuteAsync(...)`.
- **`IDisposable` scope misuse in repositories:** Repositories take `StorageDbContext` via DI;
  they never `Dispose()` the context — the DI scope owns lifetime.
- **Omitting `IgnoreQueryFilters()` in hard-delete path:** `HardDeleteAsync` in `FileRepository`
  must call `.IgnoreQueryFilters()` to find soft-deleted rows (admin operation).
- **Using `b.HasCheckConstraint(...)` directly on entity builder:** Deprecated in EF Core 7+.
  Use `b.ToTable("TableName", t => t.HasCheckConstraint(...))` — the `TreatWarningsAsErrors=true`
  project setting causes the build to fail on the obsolete overload.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Optimistic concurrency token | Custom `RowVersion` comparison logic | `IsRowVersion()` shadow property | EF throws `DbUpdateConcurrencyException` automatically; handles the `WHERE` clause in UPDATE |
| Soft-delete filter | Per-query `Where(f => f.Status != Deleted)` | `HasQueryFilter()` on entity | Global filter is applied automatically; callers never forget it |
| JSON array serialization for string[] | Manual JSON in every setter | `HasConversion` with `JsonSerializer` | Standard pattern; single place to change serializer options |
| Retry-aware transactions | Manual retry loop | `CreateExecutionStrategy().ExecuteAsync()` | Handles transient SQL errors including deadlocks with correct backoff |
| Migration runner at startup | Custom migration code | `database.MigrateAsync()` at app startup or init container | Idempotent; EF tracks applied migrations in `__EFMigrationsHistory` |
| FileCategory seed with shadow props | `HasData()` with anonymous types | `IDbSeeder` runtime seeder | 7+ shadow properties make anonymous-type `HasData` brittle; seeder is easier to read and test |

---

## Common Pitfalls

### Pitfall 1: EnableRetryOnFailure + User Transactions
**What goes wrong:** Calling `BeginTransactionAsync` directly when `EnableRetryOnFailure` is configured
causes `InvalidOperationException`: "The configured execution strategy does not support user-initiated
transactions."
**Why it happens:** The SQL Server retry strategy needs to control transaction scope to correctly
retry on transient failure. A user-opened transaction cannot be replayed.
**How to avoid:** Always wrap `BeginTransactionAsync` inside
`_db.Database.CreateExecutionStrategy().ExecuteAsync(async () => { ... })`.
**Warning signs:** Works fine in local dev (no transient failures) but fails in CI or staging on
first retry.

### Pitfall 2: OwnsOne Null-Tracking for Nullable Value Objects
**What goes wrong:** Using `OwnsOne` for nullable `StorageKey?` on `File` causes EF to generate
SQL `LEFT JOIN` and nullable tracking that throws `NullReferenceException` when the VOs are null
(pending files have no StorageKey yet).
**How to avoid:** Use `HasConversion` on the property with a null-check in the converter lambda.
EF stores the raw string as nullable; the VO is constructed only when the raw value is non-null.

### Pitfall 3: Testcontainers EULA Requirement
**What goes wrong:** `MsSqlContainer` startup fails silently (container exits immediately) if
`ACCEPT_EULA=Y` environment variable is not set.
**How to avoid:** The official `Testcontainers.MsSql` module sets `ACCEPT_EULA=Y` automatically
when using `MsSqlBuilder`. Do NOT use a generic container — use `new MsSqlBuilder().Build()`.
**Warning signs:** Container starts and immediately exits; test hangs waiting for SQL Server to be ready.

### Pitfall 4: `dotnet ef` Cannot Find DbContext Without Design-Time Factory
**What goes wrong:** `dotnet ef migrations add` fails with "Unable to create an object of type
'StorageDbContext'" if no `IDesignTimeDbContextFactory` and no startup project with DI wiring exists.
**How to avoid:** Always add `StorageDbContextFactory.cs` to the persistence project. The factory
reads a connection string from an env var (CI/CD) or falls back to the local Docker string.

### Pitfall 5: Composite-PK entities with inherited EntityBase.Id
**What goes wrong:** `FilePermission`, `FileTag`, `FileVersion` inherit `EntityBase.Id` (Guid).
If not ignored, EF adds a duplicate `Id` column plus the composite PK, causing migration errors.
**How to avoid:** Call `b.Ignore(e => e.Id)` before `b.HasKey(...)` in each configuration class.

### Pitfall 6: EntityBase.DomainEvents mapped as navigation
**What goes wrong:** All entities inheriting `EntityBase` expose `IReadOnlyList<DomainEvent> DomainEvents`.
EF Core's convention scanner treats `DomainEvent` as an entity type (no PK defined) and throws
`InvalidOperationException` at model building.
**How to avoid:** Call `b.Ignore(e => e.DomainEvents)` in EVERY entity configuration class. This
is required for File, FilePermission, FileVersion, FileTag, and AuditEntry.

### Pitfall 7: `HasCheckConstraint` obsolete under TreatWarningsAsErrors
**What goes wrong:** Calling `b.HasCheckConstraint(...)` directly on `EntityTypeBuilder` is
deprecated since EF Core 7. With `TreatWarningsAsErrors=true` (project setting), the build fails.
**How to avoid:** Use the `ToTable` overload:
`b.ToTable("TableName", t => t.HasCheckConstraint("CK_Name", "expression"))`.

### Pitfall 8: Missing `IgnoreQueryFilters()` in hard-delete path
**What goes wrong:** `FileRepository.GetByIdAsync` respects the soft-delete filter, so the admin
hard-delete path cannot find files in `deleted` status — returns null.
**How to avoid:** `HardDeleteAsync` must use `.IgnoreQueryFilters()` to locate its target row.

### Pitfall 9: AuditLog BIGINT identity PK mismatch with EntityBase.Id
**What goes wrong:** `EntityBase.Id` is Guid. If EF tries to use it as the PK for AuditLog (which
expects `BIGINT IDENTITY`), migrations generate a `UNIQUEIDENTIFIER` PK column.
**How to avoid:** Call `b.Ignore(a => a.Id)` then declare shadow property
`b.Property<long>("AuditLogId").ValueGeneratedOnAdd()` and `b.HasKey("AuditLogId")`.

---

## Code Examples

### ServiceCollectionExtensions.AddPersistence

```csharp
// Source: §10.6 storage-microservice-architecture.md
public static IServiceCollection AddPersistence(
    this IServiceCollection services, IConfiguration cfg)
{
    services.AddDbContext<StorageDbContext>((sp, opt) =>
        opt.UseSqlServer(
                cfg.GetConnectionString("Database"),
                sql => sql
                    .MigrationsAssembly("Storage.Infrastructure.Persistence.SqlServer")
                    .EnableRetryOnFailure(maxRetryCount: 3))
           .AddInterceptors(sp.GetRequiredService<SoftDeleteInterceptor>()));

    services.AddSingleton<SoftDeleteInterceptor>();
    services.AddScoped<IUnitOfWork, EfUnitOfWork>();
    services.AddScoped<IFileRepository, FileRepository>();
    services.AddScoped<IFileCategoryRepository, FileCategoryRepository>();
    services.AddScoped<IFileVersionRepository, FileVersionRepository>();
    services.AddScoped<IPermissionRepository, PermissionRepository>();
    services.AddScoped<IAuditRepository, AuditRepository>();
    services.AddScoped<FileCategorySeeder>();
    return services;
}

// Called from app startup after database.MigrateAsync():
// await scope.ServiceProvider.GetRequiredService<FileCategorySeeder>().SeedAsync(ct);
```

### Testcontainers Integration Test Fixture

```csharp
// Source: https://dotnet.testcontainers.org/modules/mssql/
public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        var opts = new DbContextOptionsBuilder<StorageDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        await using var db = new StorageDbContext(opts);
        await db.Database.MigrateAsync();
        await new FileCategorySeeder(db).SeedAsync(CancellationToken.None);
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `OwnsOne` for single-property VOs | `HasConversion` or `ComplexProperty` | EF Core 8+ | Simpler, no null-tracking gotchas |
| Per-query soft-delete `Where` clause | `HasQueryFilter` global filter | EF Core 2.0+ | Callers cannot forget; opt-in via `IgnoreQueryFilters()` |
| Raw `BeginTransactionAsync` with retries | `CreateExecutionStrategy().ExecuteAsync()` | EF Core 1.x | Correct retry semantics for SQL Server transient errors |
| `Database.EnsureCreated()` | `Database.MigrateAsync()` | EF Core 1.0 | `EnsureCreated` bypasses migrations history |
| Manual JSON serialization columns | `HasConversion` with `JsonSerializer` | EF Core 5.0+ | Single point of serialization logic |
| `b.HasCheckConstraint(...)` direct | `b.ToTable(t => t.HasCheckConstraint(...))` | EF Core 7+ | Old overload deprecated; obsolete warning fails under TreatWarningsAsErrors |
| `HasData()` with anonymous objects for shadow props | `IDbSeeder` runtime seeder | EF Core 5.0+ (best practice) | Anonymous-type `HasData` works but is brittle with many shadow properties |

**Deprecated/outdated:**
- `Database.EnsureCreated()`: Does not apply migrations; use `MigrateAsync()` in production paths.
- `[Timestamp]` attribute for RowVersion: Works but shadow-property + `IsRowVersion()` keeps the
  domain entity free of EF attributes.
- `b.HasCheckConstraint(name, sql)` directly on EntityTypeBuilder: Deprecated EF 7+; use ToTable overload.

---

## Open Questions

1. **`StorageBucket` shadow property population**
   - What we know: `Files.StorageBucket VARCHAR(128) NOT NULL` exists in §6.3 but is absent from
     the domain entity. Phase 3 defaults it to empty string via `HasDefaultValue("")`.
   - What's unclear: Storage adapters (Phase 4) need to write the actual bucket name. This will
     require Phase 4 to update the shadow property via `_db.Entry(file).Property("StorageBucket")`.
   - Recommendation: Seed as empty string; add a TODO comment. Phase 4 research should cover this.

---

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit v3 3.2.* + FluentAssertions 8.9.* |
| Config file | `backend/tests/Storage.Infrastructure.Persistence.SqlServer.Tests/Storage.Infrastructure.Persistence.SqlServer.Tests.csproj` (Wave 0 gap — does not exist yet) |
| Quick run command | `dotnet test backend/tests/Storage.Infrastructure.Persistence.SqlServer.Tests/ -x --no-build` |
| Full suite command | `dotnet test backend/tests/Storage.Infrastructure.Persistence.SqlServer.Tests/` |

### Phase Requirements to Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| PERSIST-01 | DbSets exist and context builds | smoke | `dotnet build backend/src/Storage.Infrastructure.Persistence.SqlServer/ -warnaserror` | ❌ Wave 0 |
| PERSIST-02 | Schema columns, indexes, check constraints match §6.3 | integration | `dotnet test ... -x --filter "MigrationTests"` | ❌ Wave 0 |
| PERSIST-03 | Migration applies to blank DB; seeder is idempotent | integration | `dotnet test ... -x --filter "MigrationTests.AppliesCleanly\|SeedIsIdempotent"` | ❌ Wave 0 |
| PERSIST-04 | ExecuteInTransactionAsync rolls back on failure | integration | `dotnet test ... -x --filter "TransactionTests"` | ❌ Wave 0 |
| PERSIST-05 | Soft-delete filter excludes deleted rows; concurrency conflict throws | integration | `dotnet test ... -x --filter "FileRepositoryTests\|SoftDeleteTests"` | ❌ Wave 0 |

### Sampling Rate

- **Per task commit:** `dotnet build backend/src/Storage.Infrastructure.Persistence.SqlServer/ -warnaserror`
- **Per wave merge:** `dotnet test backend/tests/Storage.Infrastructure.Persistence.SqlServer.Tests/`
- **Phase gate:** Full integration suite green before `/gsd:verify-work`

### Wave 0 Gaps

- [ ] `backend/tests/Storage.Infrastructure.Persistence.SqlServer.Tests/Storage.Infrastructure.Persistence.SqlServer.Tests.csproj` — covers PERSIST-01 through PERSIST-05
- [ ] `backend/tests/Storage.Infrastructure.Persistence.SqlServer.Tests/SqlServerFixture.cs` — Testcontainers MsSqlContainer + MigrateAsync + FileCategorySeeder
- [ ] `backend/tests/Storage.Infrastructure.Persistence.SqlServer.Tests/MigrationTests.cs` — covers PERSIST-02, PERSIST-03
- [ ] `backend/tests/Storage.Infrastructure.Persistence.SqlServer.Tests/FileRepositoryTests.cs` — covers PERSIST-05
- [ ] `backend/tests/Storage.Infrastructure.Persistence.SqlServer.Tests/TransactionTests.cs` — covers PERSIST-04
- [ ] `backend/tests/Storage.Infrastructure.Persistence.SqlServer.Tests/SoftDeleteTests.cs` — covers PERSIST-05
- [ ] Add project to `backend/StorageService.sln`
- [ ] Framework install: `dotnet add package Testcontainers.MsSql --version 4.11.0` — test project only

---

## Sources

### Primary (HIGH confidence)

- `storage-microservice-architecture.md §6.1, §6.3, §10.6` — canonical schema and DbContext skeleton
- `CLAUDE.md` — status=deleted is the soft-delete signal
- Phase 2 SUMMARY.md, all port interface files — exact repository method signatures
- Domain entity files — frozen contracts that cannot be modified
- [NuGet Gallery — Microsoft.EntityFrameworkCore.SqlServer 10.0.8](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.SqlServer/) — confirmed current version
- [NuGet Gallery — Testcontainers.MsSql 4.11.0](https://www.nuget.org/packages/Testcontainers.MsSql) — confirmed current version

### Secondary (MEDIUM confidence)

- [EF Core Connection Resiliency — Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency) — `CreateExecutionStrategy` requirement with user transactions
- [Testcontainers MsSql module — dotnet.testcontainers.org](https://dotnet.testcontainers.org/modules/mssql/) — `MsSqlBuilder` API
- [Design-time DbContext Creation — Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/cli/dbcontext-creation) — `IDesignTimeDbContextFactory` pattern
- [EF Core Tools 10.0.6 design package dependency change](https://github.com/dotnet/efcore/issues/38124) — explicit `Microsoft.EntityFrameworkCore.Design` reference required from 10.0.6

### Tertiary (LOW confidence — flagged for validation)

- EF Core 10 primitive collection mapping to JSON: native JSON column only on SQL Server 2025 compat level 170; SQL Server 2022 Docker requires explicit `HasConversion` — from WebSearch, needs build verification.
- `HasCheckConstraint` deprecated since EF Core 7 — from search results; verify the exact EF 10 behavior under TreatWarningsAsErrors before execution.

---

## Metadata

**Confidence breakdown:**

- Standard stack: HIGH — NuGet versions confirmed via search (10.0.8 GA, Testcontainers 4.11.0)
- Domain-schema bridge table: HIGH — derived directly from reading all domain entity files + §6.3 SQL
- Architecture patterns: HIGH — directly from §10.6 plus official EF Core docs
- Pitfalls: HIGH — CreateExecutionStrategy from official docs; DomainEvents ignore from entity analysis; HasCheckConstraint deprecation from EF GitHub
- Seeder via IDbSeeder vs HasData: MEDIUM — correct decision but exact shadow-prop seeder syntax should be verified at build time

**Research date:** 2026-05-16
**Valid until:** 2026-06-16 (EF Core 10.x patch releases; validate NuGet version before final build)
