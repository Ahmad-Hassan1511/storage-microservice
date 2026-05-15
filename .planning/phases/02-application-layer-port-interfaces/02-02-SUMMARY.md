---
phase: 02-application-layer-port-interfaces
plan: 02-02
status: complete
completed_at: 2026-05-16
---

# Plan 02-02 Summary — UploadService TDD

## What was delivered

- `backend/src/Storage.Application/Services/UploadService.cs` — `InitiateUploadAsync` and `CompleteUploadAsync` use-case orchestration
- `backend/tests/Storage.Application.Tests/UploadServiceTests.cs` — 12 green tests covering APP-02, APP-03, APP-04

## Key decisions and deviations

**Inline policy checks instead of `category.Validate()`:** `FileCategory.Validate()` returns a single `(bool, string?)` pair that cannot produce distinct 413 (size) vs 415 (MIME/extension) HTTP status hints. UploadService inlines the three checks directly against `req` values, preserving the error semantics without delegating to the domain entity.

**Two-key idempotency cache:** The idempotency state uses two cache keys per `IdempotencyKey`:
- `idempotency:{key}:hash` → `string` (SHA-256 hex of the payload)
- `idempotency:{key}:response` → `InitiateUploadResponse`

This avoids a private nested `IdempotencyRecord` type that would be inaccessible to the test project, keeping the NSubstitute mock setup clean.

**Payload hash includes OwnerService:** The SHA-256 input is `"{CategoryId}|{OriginalFileName}|{SizeBytes}|{OwnerService}"`. This prevents two services from colliding on the same `IdempotencyKey` when uploading identical files under different identities.

**AllowedOwnerServices checked before policy validation:** The access check on `req.OwnerService` runs immediately after category resolution and before any size/MIME/extension checks, matching the plan's prescribed order.

## Tests

All 12 `UploadServiceTests` pass (no skips). Full suite: 67 tests, 0 failed.
