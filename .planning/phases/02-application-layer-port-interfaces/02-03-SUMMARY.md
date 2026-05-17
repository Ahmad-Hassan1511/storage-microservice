---
phase: 02-application-layer-port-interfaces
plan: "03"
subsystem: application-layer
tags: [download, presigned-url, proxy-stream, acl, cache, tdd]
dependency_graph:
  requires: [02-01]
  provides: [DownloadService, APP-05, APP-06]
  affects: [Storage.Api, Phase-3-Persistence]
tech_stack:
  added: []
  patterns: [cache-read-through, status-gate-404, anti-enumeration-404, acl-permission-enum]
key_files:
  created:
    - backend/src/Storage.Application/Services/DownloadService.cs
  modified:
    - backend/tests/Storage.Application.Tests/DownloadServiceTests.cs
    - backend/src/Storage.Domain/Entities/AuditEntry.cs
    - backend/src/Storage.Domain/Entities/FilePermission.cs
    - backend/src/Storage.Domain/Entities/File.cs
decisions:
  - "ACL uses Permission.Read enum (not CanRead bool) matching actual FilePermission entity shape"
  - "Anti-enumeration: cross-tenant and non-ready both return NotFoundError (404 not 403)"
  - "PreviewUrl/ThumbnailUrl left null — second StorageKey lookup out of scope for this plan"
  - "File.Rehydrate() factory added to Domain for persistence rehydration and test fixtures"
metrics:
  duration: "25 min"
  completed_date: "2026-05-16"
  tasks_completed: 1
  files_modified: 5
requirements: [APP-05, APP-06]
---

# Phase 2 Plan 03: DownloadService Summary

**One-liner:** Cache-first presigned URL service with status-gate (404), ACL (Permission.Read enum), anti-enumeration, and audited proxy stream fallback using TDD.

## What Was Built

`DownloadService` is the read side of the hexagon. It enforces three access control axes in order:
1. **Tenant isolation** — `GetByIdAsync(fileId, caller.TenantId)` scopes the query; null = 404 (anti-enumeration)
2. **Status gate** — non-Ready files (Pending, Scanning, Quarantined, Deleted) return 404, never served
3. **ACL** — scans `file.Permissions` for `(PrincipalType, PrincipalId, Permission.Read)`; Public files skip ACL

`GetFileAsync` returns a 15-minute presigned URL cached for 5 minutes under `file:{fileId}`.
`GetFileStreamAsync` writes an `AuditEntry` (action=proxy-download), saves, then calls `OpenReadStreamAsync`.

## Deviations from Plan

### Auto-fixed Issues (Rule 2 — Missing Critical Functionality)

**1. [Rule 2 - Missing Factory] Added AuditEntry.Create() factory**
- **Found during:** RED phase — AuditEntry had all private setters, no public constructor
- **Issue:** DownloadService could not construct AuditEntry from Application layer
- **Fix:** Added `AuditEntry.Create(fileId, action, performedBy, performedAt, details?)` static factory
- **Files modified:** `backend/src/Storage.Domain/Entities/AuditEntry.cs`
- **Commit:** 180c62d

**2. [Rule 2 - Missing Factory] Added FilePermission.Create() factory**
- **Found during:** RED phase — FilePermission had all private setters
- **Issue:** Test fixtures needed to construct FilePermission with known values
- **Fix:** Added `FilePermission.Create(fileId, principalType, principalId, permission)` static factory
- **Files modified:** `backend/src/Storage.Domain/Entities/FilePermission.cs`
- **Commit:** 180c62d

**3. [Rule 2 - Missing Factory] Added File.Rehydrate() factory**
- **Found during:** RED phase — File.Create() only produces Pending status with no StorageKey or Permissions
- **Issue:** TDD tests need Ready-status files with populated Permissions and StorageKey
- **Fix:** Added `File.Rehydrate(...)` factory for persistence adapters and test fixtures
- **Files modified:** `backend/src/Storage.Domain/Entities/File.cs`
- **Commit:** 180c62d

**4. [Rule 1 - Schema Mismatch] FilePermission.CanRead does not exist**
- **Found during:** Plan analysis — plan's interface block specified `permission.CanRead (bool)`
- **Issue:** Actual entity uses `Permission Permission` enum (Read | Write | Delete)
- **Fix:** ACL check uses `p.Permission == Permission.Read` throughout DownloadService
- **Files modified:** `backend/src/Storage.Application/Services/DownloadService.cs`

## Test Results

```
Passed! - Failed: 0, Passed: 5, Skipped: 0, Total: 5
```

All 5 DownloadService tests pass. Build exits 0 with 0 warnings.

## Self-Check: PASSED

- `backend/src/Storage.Application/Services/DownloadService.cs` — FOUND
- `backend/tests/Storage.Application.Tests/DownloadServiceTests.cs` — FOUND
- Commit 180c62d (RED) — FOUND
- Commit 641be30 (GREEN) — FOUND
