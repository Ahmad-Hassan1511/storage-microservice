---
phase: 3
slug: persistence-adapter
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-05-16
---

# Phase 3 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3 3.2.* + FluentAssertions 8.9.* + Testcontainers.MsSql 4.11.0 |
| **Config file** | `backend/tests/Storage.Infrastructure.Persistence.SqlServer.Tests/Storage.Infrastructure.Persistence.SqlServer.Tests.csproj` (Wave 0 creates) |
| **Quick run command** | `dotnet build backend/src/Storage.Infrastructure.Persistence.SqlServer/ -warnaserror` |
| **Full suite command** | `dotnet test backend/tests/Storage.Infrastructure.Persistence.SqlServer.Tests/ -v minimal` |
| **Estimated runtime** | ~60 seconds (Testcontainers pulls SQL Server 2022 image on first run) |

---

## Sampling Rate

- **After every task commit:** `dotnet build backend/src/Storage.Infrastructure.Persistence.SqlServer/ -warnaserror`
- **After every plan wave:** `dotnet test backend/tests/Storage.Infrastructure.Persistence.SqlServer.Tests/ -v minimal`
- **Before `/gsd:verify-work`:** Full integration suite must be green
- **Max feedback latency:** 90 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 3-01-01 | 01 | 1 | PERSIST-01 | build | `dotnet build backend/src/Storage.Infrastructure.Persistence.SqlServer/ -warnaserror` | ❌ W0 | ⬜ pending |
| 3-01-02 | 01 | 1 | PERSIST-02 | build | `dotnet build backend/src/Storage.Infrastructure.Persistence.SqlServer/ -warnaserror` | ❌ W0 | ⬜ pending |
| 3-01-03 | 01 | 1 | PERSIST-01 | build | `dotnet build backend/src/Storage.Infrastructure.Persistence.SqlServer/ -warnaserror` | ❌ W0 | ⬜ pending |
| 3-01-04 | 01 | 1 | PERSIST-04 | build | `dotnet build backend/src/Storage.Infrastructure.Persistence.SqlServer/ -warnaserror` | ❌ W0 | ⬜ pending |
| 3-01-05 | 01 | 1 | PERSIST-05 | build | `dotnet build backend/src/Storage.Infrastructure.Persistence.SqlServer/ -warnaserror` | ❌ W0 | ⬜ pending |
| 3-02-01 | 02 | 2 | PERSIST-02 | integration | `dotnet test backend/tests/Storage.Infrastructure.Persistence.SqlServer.Tests/ --filter "MigrationTests" -v minimal` | ❌ W0 | ⬜ pending |
| 3-02-02 | 02 | 2 | PERSIST-03 | integration | `dotnet test backend/tests/Storage.Infrastructure.Persistence.SqlServer.Tests/ --filter "MigrationTests" -v minimal` | ❌ W0 | ⬜ pending |
| 3-02-03 | 02 | 2 | PERSIST-04 | integration | `dotnet test backend/tests/Storage.Infrastructure.Persistence.SqlServer.Tests/ --filter "TransactionTests" -v minimal` | ❌ W0 | ⬜ pending |
| 3-02-04 | 02 | 2 | PERSIST-05 | integration | `dotnet test backend/tests/Storage.Infrastructure.Persistence.SqlServer.Tests/ --filter "SoftDeleteTests\|FileRepositoryTests" -v minimal` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `backend/tests/Storage.Infrastructure.Persistence.SqlServer.Tests/Storage.Infrastructure.Persistence.SqlServer.Tests.csproj` — xUnit v3 + FluentAssertions + Testcontainers.MsSql
- [ ] `backend/tests/Storage.Infrastructure.Persistence.SqlServer.Tests/SqlServerFixture.cs` — Testcontainers MsSqlContainer + MigrateAsync + FileCategorySeeder (stubs)
- [ ] `backend/tests/Storage.Infrastructure.Persistence.SqlServer.Tests/MigrationTests.cs` — 2 stubs: SchemaCreatedCleanly, SeedIsIdempotent
- [ ] `backend/tests/Storage.Infrastructure.Persistence.SqlServer.Tests/FileRepositoryTests.cs` — 3 stubs: GetById_IncludesPermissions, ListAsync_CursorPagination, HardDelete_FindsSoftDeleted
- [ ] `backend/tests/Storage.Infrastructure.Persistence.SqlServer.Tests/TransactionTests.cs` — 1 stub: ExecuteInTransaction_RollsBackOnFailure
- [ ] `backend/tests/Storage.Infrastructure.Persistence.SqlServer.Tests/SoftDeleteTests.cs` — 2 stubs: SoftDeletedFilesExcludedFromQuery, ConcurrencyToken_ThrowsOnConflict
- [ ] Add `Storage.Infrastructure.Persistence.SqlServer.Tests` to `backend/StorageService.sln`

*Wave 0 creates stub test files before any production infrastructure code is written.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| `dotnet ef migrations add Initial` generates correct SQL | PERSIST-03 | Requires local docker + dotnet-ef tool | Run `dotnet ef migrations add Initial -p backend/src/Storage.Infrastructure.Persistence.SqlServer/` with SQL Server running; inspect generated SQL |
| `StorageBucket` default empty string is present in migration SQL | PERSIST-02 | Schema audit | Open generated migration file; verify `StorageBucket` column with `defaultValue: ""` |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 90s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
