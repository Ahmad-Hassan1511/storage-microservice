---
phase: 2
slug: application-layer-port-interfaces
status: draft
nyquist_compliant: true
wave_0_complete: true
created: 2026-05-16
---

# Phase 2 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3 3.2.* via `dotnet test` |
| **Config file** | `backend/StorageService.sln` — Wave 0 creates `Storage.Application.Tests` |
| **Quick run command** | `dotnet test backend/tests/Storage.Application.Tests -v minimal` |
| **Full suite command** | `dotnet test backend/tests/ -v minimal` |
| **Estimated runtime** | ~15 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test backend/tests/Storage.Application.Tests -v minimal`
- **After every plan wave:** Run `dotnet test backend/tests/ -v minimal`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 30 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 2-01-01 | 01 | 1 | APP-01 | build | `dotnet build backend/src/Storage.Application/` | ❌ W0 | ⬜ pending |
| 2-02-01 | 02 | 2 | APP-02 | unit | `dotnet test --filter "FullyQualifiedName~UploadServiceTests.InitiateUpload_ValidRequest"` | ❌ W0 | ⬜ pending |
| 2-02-02 | 02 | 2 | APP-02 | unit | `dotnet test --filter "FullyQualifiedName~UploadServiceTests.InitiateUpload_UnknownCategory"` | ❌ W0 | ⬜ pending |
| 2-02-03 | 02 | 2 | APP-02 | unit | `dotnet test --filter "FullyQualifiedName~UploadServiceTests.InitiateUpload_OversizeFile"` | ❌ W0 | ⬜ pending |
| 2-02-04 | 02 | 2 | APP-02 | unit | `dotnet test --filter "FullyQualifiedName~UploadServiceTests.InitiateUpload_DisallowedMimeType"` | ❌ W0 | ⬜ pending |
| 2-02-05 | 02 | 2 | APP-02 | unit | `dotnet test --filter "FullyQualifiedName~UploadServiceTests.InitiateUpload_DisallowedExtension"` | ❌ W0 | ⬜ pending |
| 2-02-06 | 02 | 2 | APP-02 | unit | `dotnet test --filter "FullyQualifiedName~UploadServiceTests.InitiateUpload_ForbiddenOwnerService"` | ❌ W0 | ⬜ pending |
| 2-02-07 | 02 | 2 | APP-02 | unit | `dotnet test --filter "FullyQualifiedName~UploadServiceTests.InitiateUpload_LargeFileCategory_MultipartRequired"` | ❌ W0 | ⬜ pending |
| 2-02-08 | 02 | 2 | APP-03 | unit | `dotnet test --filter "FullyQualifiedName~UploadServiceTests.InitiateUpload_SameKeyAndPayload_ReturnsExistingFileId"` | ❌ W0 | ⬜ pending |
| 2-02-09 | 02 | 2 | APP-03 | unit | `dotnet test --filter "FullyQualifiedName~UploadServiceTests.InitiateUpload_SameKeyDifferentPayload_ReturnsIdempotencyConflictError"` | ❌ W0 | ⬜ pending |
| 2-02-09b | 02 | 2 | APP-03 | unit | `dotnet test --filter "FullyQualifiedName~UploadServiceTests.InitiateUpload_SameKeyDifferentOwnerService_ReturnsIdempotencyConflictError"` | ❌ W0 | ⬜ pending |
| 2-02-10 | 02 | 2 | APP-04 | unit | `dotnet test --filter "FullyQualifiedName~UploadServiceTests.CompleteUpload_ValidChecksum_TransitionsToScanning"` | ❌ W0 | ⬜ pending |
| 2-02-11 | 02 | 2 | APP-04 | unit | `dotnet test --filter "FullyQualifiedName~UploadServiceTests.CompleteUpload_ChecksumMismatch_ReturnsChecksumMismatchError"` | ❌ W0 | ⬜ pending |
| 2-02-12 | 02 | 2 | APP-05 | unit | `dotnet test --filter "FullyQualifiedName~DownloadServiceTests.GetFile_ReadyFile_ReturnsPresignedUrl"` | ❌ W0 | ⬜ pending |
| 2-02-13 | 02 | 2 | APP-05 | unit | `dotnet test --filter "FullyQualifiedName~DownloadServiceTests.GetFile_PendingFile_ReturnsNotFound"` | ❌ W0 | ⬜ pending |
| 2-02-14 | 02 | 2 | APP-05 | unit | `dotnet test --filter "FullyQualifiedName~DownloadServiceTests.GetFile_CrossTenant_ReturnsNotFound"` | ❌ W0 | ⬜ pending |
| 2-02-15 | 02 | 2 | APP-05 | unit | `dotnet test --filter "FullyQualifiedName~DownloadServiceTests.GetFile_InsufficientPermission_ReturnsAccessDenied"` | ❌ W0 | ⬜ pending |
| 2-02-16 | 02 | 2 | APP-06 | unit | `dotnet test --filter "FullyQualifiedName~DownloadServiceTests.GetFileStream_ProxyPath_CallsOpenReadStream"` | ❌ W0 | ⬜ pending |
| 2-02-17 | 02 | 2 | APP-07 | unit | `dotnet test --filter "FullyQualifiedName~FileManagementServiceTests.SoftDelete_ReadyFile_SetsDeletedStatus"` | ❌ W0 | ⬜ pending |
| 2-02-18 | 02 | 2 | APP-07 | unit | `dotnet test --filter "FullyQualifiedName~FileManagementServiceTests.HardDelete_WithoutAdminScope_ReturnsAccessDenied"` | ❌ W0 | ⬜ pending |
| 2-02-19 | 02 | 2 | APP-07 | unit | `dotnet test --filter "FullyQualifiedName~FileManagementServiceTests.ListFiles_WithCursor_ReturnsPaginatedResults"` | ❌ W0 | ⬜ pending |
| 2-02-20 | 04 | 2 | APP-07 | unit | `dotnet test --filter "FullyQualifiedName~FileManagementServiceTests.PatchFile_UpdatesMetadataAndInvalidatesCache"` | ❌ W0 | ⬜ pending |
| 2-02-21 | 04 | 2 | APP-07 | unit | `dotnet test --filter "FullyQualifiedName~FileManagementServiceTests.CreateVersion_AddsFileVersionEntry"` | ❌ W0 | ⬜ pending |
| 2-02-22 | 04 | 2 | APP-07 | unit | `dotnet test --filter "FullyQualifiedName~FileManagementServiceTests.GenerateShareLink_StoresTokenInCache"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `backend/tests/Storage.Application.Tests/Storage.Application.Tests.csproj` — xUnit v3 + NSubstitute + FluentAssertions + coverlet
- [ ] `backend/tests/Storage.Application.Tests/UploadServiceTests.cs` — 12 stubs for APP-02, APP-03, APP-04 (includes OwnerService idempotency variant)
- [ ] `backend/tests/Storage.Application.Tests/DownloadServiceTests.cs` — 5 stubs for APP-05, APP-06
- [ ] `backend/tests/Storage.Application.Tests/FileManagementServiceTests.cs` — 6 stubs for APP-07 (includes PatchFile, CreateVersion, GenerateShareLink)

*Wave 0 creates the test project and stub files before any application service code is written.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| No infrastructure assembly referenced at compile time | APP-01 | Build check only; confirmed by `dotnet build` exit code | `dotnet build backend/src/Storage.Application/ -warnaserror` returns 0 |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 30s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
