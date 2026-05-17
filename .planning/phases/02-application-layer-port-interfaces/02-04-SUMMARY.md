---
phase: 02-application-layer-port-interfaces
plan: "04"
subsystem: application-services
tags: [file-management, soft-delete, hard-delete, patch, versioning, share-links, tdd]
dependency_graph:
  requires: [02-01]
  provides: [FileManagementService, PatchFileRequest]
  affects: [Storage.Domain.Entities.File, Storage.Domain.Entities.FileTag, Storage.Domain.Entities.FileVersion]
tech_stack:
  added: []
  patterns: [cursor-pagination, cache-invalidation, admin-scope-gate, url-safe-base64-token]
key_files:
  created:
    - backend/src/Storage.Application/Services/FileManagementService.cs
    - backend/src/Storage.Application/DTOs/PatchFileRequest.cs
  modified:
    - backend/src/Storage.Domain/Entities/File.cs
    - backend/src/Storage.Domain/Entities/FileTag.cs
    - backend/src/Storage.Domain/Entities/FileVersion.cs
    - backend/tests/Storage.Application.Tests/FileManagementServiceTests.cs
decisions:
  - FileVersion.Create factory wraps string storageKey/checksumSha256 into value objects — consistent with File.Create pattern
  - FileTag public constructor added to support UpdateTags mutation without leaking EF concerns into domain logic
  - CreateVersionAsync takes sizeBytes parameter (plan omitted it but FileVersion entity requires it)
  - DownloadService was already implemented on disk (from a prior parallel wave) — not re-created
metrics:
  duration: "18 min"
  completed_date: "2026-05-16"
  tasks_completed: 2
  files_created: 2
  files_modified: 4
---

# Phase 2 Plan 4: FileManagementService Summary

**One-liner:** FileManagementService with six write-side use cases (soft-delete, admin-gated hard-delete, cursor-paginated listing, metadata patch with cache invalidation, version creation, share-link token generation) backed by 6 green TDD tests.

---

## Tasks Completed

| Task | Name | Commit | Key Files |
|------|------|--------|-----------|
| 1 | Amend File.cs — patch mutators + domain fixes | ad52cbd | File.cs, FileTag.cs, FileVersion.cs |
| 2 | Implement FileManagementService (TDD) | 5c63e2e | FileManagementService.cs, PatchFileRequest.cs, FileManagementServiceTests.cs |

---

## Verification Results

- `dotnet build backend/src/Storage.Domain/ -warnaserror` — 0 errors, 0 warnings
- `dotnet build backend/src/Storage.Application/ -warnaserror` — 0 errors, 0 warnings
- `dotnet test --filter FileManagementServiceTests` — 6 passed, 0 failed, 0 skipped
- `dotnet test backend/tests/Storage.Application.Tests/` — 11 passed, 12 skipped (UploadService stubs), 0 failed

---

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] FileVersion had no public constructor or factory**
- **Found during:** Task 2 GREEN phase
- **Issue:** Plan calls `new FileVersion(fileId, versionNumber, storageKey, checksumSha256, createdAt)` but FileVersion only had private setters and no public constructor
- **Fix:** Added `FileVersion.Create(Guid fileId, int versionNumber, string storageKey, string checksumSha256, long sizeBytes, DateTime createdAt)` factory; strings wrapped into StorageKey/Checksum value objects; added private EF Core constructor
- **Files modified:** backend/src/Storage.Domain/Entities/FileVersion.cs
- **Commit:** ad52cbd

**2. [Rule 3 - Blocking] FileTag had no public constructor**
- **Found during:** Task 1 — `UpdateTags` calls `new FileTag(key, value)`
- **Issue:** FileTag only had implicit constructor; all setters private
- **Fix:** Added `FileTag(string key, string value)` public constructor + private EF constructor
- **Files modified:** backend/src/Storage.Domain/Entities/FileTag.cs
- **Commit:** ad52cbd

**3. [Rule 1 - Bug] CreateVersionAsync missing sizeBytes parameter**
- **Found during:** Task 2 — FileVersion.SizeBytes required by entity
- **Issue:** Plan's `CreateVersionAsync` signature omitted `sizeBytes`; entity has non-optional field
- **Fix:** Added `long sizeBytes` parameter to `CreateVersionAsync`
- **Files modified:** backend/src/Storage.Application/Services/FileManagementService.cs
- **Commit:** 5c63e2e

**4. [Rule 1 - Bug] Test assertion for VersionNumber was incorrect**
- **Found during:** Task 2 GREEN phase (1 test failed)
- **Issue:** Test computed `file.Versions.Count + 1 - 1` (= 0) but service correctly returns version 1
- **Fix:** Corrected assertion to `.Be(1)` with explanatory comment (mocked AddAsync does not mutate in-memory list)
- **Files modified:** backend/tests/Storage.Application.Tests/FileManagementServiceTests.cs
- **Commit:** 5c63e2e

---

## Self-Check: PASSED

- `backend/src/Storage.Application/Services/FileManagementService.cs` — FOUND
- `backend/src/Storage.Application/DTOs/PatchFileRequest.cs` — FOUND
- `backend/tests/Storage.Application.Tests/FileManagementServiceTests.cs` — FOUND (updated)
- Commits ad52cbd and 5c63e2e — FOUND in git log
