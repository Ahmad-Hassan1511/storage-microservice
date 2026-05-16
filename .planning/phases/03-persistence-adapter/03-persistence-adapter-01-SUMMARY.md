---
phase: "03"
plan: "01"
subsystem: "Storage.Infrastructure.Persistence.SqlServer"
tags: [ef-core, sql-server, repositories, migrations, hexagonal-architecture]
dependency_graph:
  requires: [Storage.Domain, Storage.Application]
  provides: [IUnitOfWork, IFileRepository, IFileCategoryRepository, IFileVersionRepository, IPermissionRepository, IAuditRepository]
  affects: [Storage.Api]
tech_stack:
  added:
    - Microsoft.EntityFrameworkCore.SqlServer 10.0.8
    - Microsoft.EntityFrameworkCore.Design 10.0.8
    - Microsoft.EntityFrameworkCore.Tools 10.0.8
  patterns:
    - EF Core Ports and Adapters (infrastructure implements application interfaces)
    - Query filter for soft-delete on File aggregate
    - Composite PK pattern for FileVersion, FilePermission, FileTag (Id ignored)
    - Shadow BIGINT PK for AuditEntry (domain Guid Id ignored)
    - HasConversion for value objects (StorageKey, Checksum) and string[] arrays (JSON)
    - CreateExecutionStrategy for retry-safe transactions
    - IDesignTimeDbContextFactory for tooling support
key_files:
  created:
    - backend/src/Storage.Infrastructure.Persistence.SqlServer/Storage.Infrastructure.Persistence.SqlServer.csproj
    - backend/src/Storage.Infrastructure.Persistence.SqlServer/StorageDbContext.cs
    - backend/src/Storage.Infrastructure.Persistence.SqlServer/StorageDbContextFactory.cs
    - backend/src/Storage.Infrastructure.Persistence.SqlServer/Interceptors/SoftDeleteInterceptor.cs
    - backend/src/Storage.Infrastructure.Persistence.SqlServer/Configurations/FileConfiguration.cs
    - backend/src/Storage.Infrastructure.Persistence.SqlServer/Configurations/FileCategoryConfiguration.cs
    - backend/src/Storage.Infrastructure.Persistence.SqlServer/Configurations/FileVersionConfiguration.cs
    - backend/src/Storage.Infrastructure.Persistence.SqlServer/Configurations/FilePermissionConfiguration.cs
    - backend/src/Storage.Infrastructure.Persistence.SqlServer/Configurations/FileTagConfiguration.cs
    - backend/src/Storage.Infrastructure.Persistence.SqlServer/Configurations/AuditEntryConfiguration.cs
    - backend/src/Storage.Infrastructure.Persistence.SqlServer/Repositories/FileRepository.cs
    - backend/src/Storage.Infrastructure.Persistence.SqlServer/Repositories/FileCategoryRepository.cs
    - backend/src/Storage.Infrastructure.Persistence.SqlServer/Repositories/FileVersionRepository.cs
    - backend/src/Storage.Infrastructure.Persistence.SqlServer/Repositories/PermissionRepository.cs
    - backend/src/Storage.Infrastructure.Persistence.SqlServer/Repositories/AuditRepository.cs
    - backend/src/Storage.Infrastructure.Persistence.SqlServer/EfUnitOfWork.cs
    - backend/src/Storage.Infrastructure.Persistence.SqlServer/Seeders/FileCategorySeeder.cs
    - backend/src/Storage.Infrastructure.Persistence.SqlServer/Extensions/ServiceCollectionExtensions.cs
    - backend/src/Storage.Infrastructure.Persistence.SqlServer/Migrations/20260516004752_Initial.cs
    - backend/src/Storage.Infrastructure.Persistence.SqlServer/Migrations/20260516004752_Initial.Designer.cs
    - backend/src/Storage.Infrastructure.Persistence.SqlServer/Migrations/StorageDbContextModelSnapshot.cs
  modified: []
decisions:
  - StorageKey? and Checksum? use HasConversion with explicit (string?)null cast on null branch to satisfy nullable compiler analysis under -warnaserror
  - dotnet-ef 9.0.4 (globally installed) used instead of 10.0.8 — tool version mismatch advisory only; migration generated correctly against EF Core 10.0.8 runtime; upgrading tool blocked by global NuGet packageSourceMapping that excludes dotnet-ef pattern
  - HasCheckConstraint uses ToTable overload on both Files and FilePermissions tables as required
  - AuditEntry uses shadow BIGINT "AuditLogId" with IDENTITY; domain Guid Id is ignored at persistence boundary
metrics:
  duration: "~25 minutes"
  completed: "2026-05-16"
  tasks_completed: 6
  files_created: 21
---

# Phase 3 Plan 01: EF Core SQL Server Persistence Adapter Summary

EF Core 10 SQL Server persistence adapter with 6 entity configurations, 5 repositories, UnitOfWork, seeder, DI extensions, and Initial migration — all compiling at 0 warnings under `-warnaserror`.

## Tasks Completed

| Task | Description | Status |
|------|-------------|--------|
| 1 | Update csproj with EF Core 10.0.8 packages | Done |
| 2 | StorageDbContext, SoftDeleteInterceptor, DesignTimeFactory | Done |
| 3 | Six entity type configurations (Configurations/) | Done |
| 4 | Five repositories (Repositories/) | Done |
| 5 | EfUnitOfWork, FileCategorySeeder, ServiceCollectionExtensions | Done |
| 6 | Generate Initial EF Core migration | Done |

## Files Created

### Infrastructure Root
- `StorageDbContext.cs` — DbContext with 6 DbSets, ApplyConfigurationsFromAssembly
- `StorageDbContextFactory.cs` — IDesignTimeDbContextFactory for dotnet-ef tooling
- `EfUnitOfWork.cs` — IUnitOfWork implementation with CreateExecutionStrategy for retry-safe transactions

### Interceptors
- `Interceptors/SoftDeleteInterceptor.cs` — Intercepts Deleted File entries, converts to Modified with DeletedAt/UpdatedAt shadow properties

### Configurations
- `Configurations/FileConfiguration.cs` — File aggregate root; check constraint via ToTable overload; StorageKey/Checksum via HasConversion; soft-delete query filter; two self-referencing FKs; DomainEvents ignored
- `Configurations/FileCategoryConfiguration.cs` — No EntityBase; string[] arrays via JsonSerializer HasConversion; 6 shadow properties (Visibility, PreviewStrategy, ThumbnailSizes, RetentionDays, LifecycleTier, CreatedAt/UpdatedAt)
- `Configurations/FileVersionConfiguration.cs` — Composite PK (FileId, VersionNumber); Id and DomainEvents ignored; StorageKey/Checksum via HasConversion
- `Configurations/FilePermissionConfiguration.cs` — Composite PK (FileId, PrincipalType, PrincipalId, Permission); check constraint via ToTable overload; Id and DomainEvents ignored
- `Configurations/FileTagConfiguration.cs` — Composite PK (FileId, Key); Id and DomainEvents ignored
- `Configurations/AuditEntryConfiguration.cs` — Shadow BIGINT "AuditLogId" PK with IDENTITY; Guid Id and DomainEvents ignored; PerformedBy→Actor, PerformedAt→OccurredAt, Details→Metadata column mapping

### Repositories
- `Repositories/FileRepository.cs` — GetByIdAsync (eager-loads Permissions), AddAsync, ListAsync (cursor-based pagination), HardDeleteAsync (IgnoreQueryFilters)
- `Repositories/FileCategoryRepository.cs` — FindAsync by string PK, ListAllAsync ordered by DisplayName
- `Repositories/FileVersionRepository.cs` — AddAsync, GetByFileIdAsync (tenant-scoped via subquery)
- `Repositories/PermissionRepository.cs` — GetAsync, AddAsync, RemoveAsync
- `Repositories/AuditRepository.cs` — AddAsync only (append-only log)

### Seeders and Extensions
- `Seeders/FileCategorySeeder.cs` — Seeds 7 categories (image, document, video, audio, archive, spreadsheet, presentation); guards with AnyAsync; sets shadow properties via ChangeTracker after AddRange
- `Extensions/ServiceCollectionExtensions.cs` — AddSqlServerPersistence extension method; registers DbContext with retry, SoftDeleteInterceptor, all 5 repositories, IUnitOfWork, FileCategorySeeder

### Migrations
- `Migrations/20260516004752_Initial.cs` — Complete initial schema migration
- `Migrations/20260516004752_Initial.Designer.cs` — EF Core migration snapshot metadata
- `Migrations/StorageDbContextModelSnapshot.cs` — Model snapshot for incremental migration diffing

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Nullable correctness] Explicit null cast in StorageKey and Checksum HasConversion**
- **Found during:** Task 3, FileConfiguration.cs
- **Issue:** The plan's nullable conversion lambdas `sk => sk == null ? null : sk.Value` could produce CS8603 under -warnaserror because the compiler cannot infer `string?` return type from the ternary
- **Fix:** Changed null branch to `(string?)null` cast: `sk => sk == null ? (string?)null : sk.Value`
- **Files modified:** `Configurations/FileConfiguration.cs`
- **Note:** Build passed with 0 warnings — the cast was a precaution and the compiler accepted it cleanly

### dotnet-ef Tool Version

The global dotnet-ef tool is version 9.0.4. Upgrading to 10.0.8 was blocked by the global NuGet.Config packageSourceMapping which does not include the `dotnet-ef` package pattern. The 9.0.4 tool successfully generated the migration with an advisory warning only ("tools version '9.0.4' is older than runtime '10.0.8'"). The migration files are syntactically correct EF Core 10 output.

## Build Output

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

## Migration Command Output

```
Build started...
Build succeeded.
The Entity Framework tools version '9.0.4' is older than that of the runtime '10.0.8'. Update the tools for the latest features and bug fixes.
Done. To undo this action, use 'ef migrations remove'
```

## Success Criteria Verification

| Criterion | Status |
|-----------|--------|
| dotnet build -warnaserror exits 0 | PASS - 0 warnings, 0 errors |
| StorageDbContextModelSnapshot.cs exists | PASS |
| Six DbSet declarations in StorageDbContext.cs | PASS - Files, FileCategories, FileVersions, FilePermissions, FileTags, AuditLog |
| SoftDeleteInterceptor overrides both SavingChanges and SavingChangesAsync | PASS |
| EfUnitOfWork.ExecuteInTransactionAsync calls CreateExecutionStrategy | PASS |
| FileRepository.HardDeleteAsync calls IgnoreQueryFilters() | PASS |
| HasCheckConstraint uses ToTable overload for Files and FilePermissions | PASS |
| FileCategory array properties use JsonSerializer HasConversion | PASS - AllowedMimeTypes, AllowedExtensions, AllowedOwnerServices |
| AuditEntry uses shadow BIGINT "AuditLogId" PK with Ignore(Id) | PASS |
| FileCategorySeeder.SeedAsync checks AnyAsync before inserting | PASS |

## Self-Check: PASSED

All 21 files created and verified. Commit hash: f1af073.
