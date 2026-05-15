---
phase: 01-solution-scaffold-domain-model
plan: 02
subsystem: domain
tags: [dotnet, csharp, domain-driven-design, value-objects, state-machine, tdd, xunit]

# Dependency graph
requires:
  - phase: 01-01
    provides: solution scaffold with Storage.Domain project and test stubs in Storage.Domain.Tests

provides:
  - Storage.Domain project with all entities, value objects, enums, events, and exceptions
  - File aggregate root with validated status state machine (Pending->Scanning->Ready|Quarantined, Ready->Deleted)
  - StorageKey value object with regex validation and Create() factory
  - Checksum value object with SHA-256 hex validation and lowercase normalisation
  - FileCategory entity with Validate() method returning (bool, string?) result tuple
  - 6 domain event records: FileCreatedEvent, FileUploadedEvent, FileScannedEvent, FileReadyEvent, FileDeletedEvent, FilePermissionChangedEvent
  - DomainException hierarchy with InvalidStatusTransitionException, InvalidStorageKeyException, InvalidChecksumException
  - EntityBase with Guid Id, RaiseDomainEvent, ClearDomainEvents
  - 44 passing domain unit tests covering all business invariants

affects:
  - 02-application-layer (builds on domain entities and port interfaces)
  - 03-persistence-adapter (EF Core mappings for File, FileCategory, FileVersion, FilePermission, FileTag, AuditEntry)
  - 06-rest-api (consumes File aggregate, FileCategory.Validate(), File.Transition())

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "TDD RED->GREEN->REFACTOR cycle for all domain types"
    - "Ports and Adapters: Storage.Domain has zero infrastructure dependencies (pure BCL only)"
    - "EntityBase non-generic with Guid Id and DomainEvents collection"
    - "DomainEvent as abstract record base with positional EventId + OccurredAt"
    - "Result tuple (bool IsValid, string? Error) for validation — FileCategory.Validate() does NOT throw"
    - "InvalidStatusTransitionException thrown from File.Transition() — different pattern than validation"
    - "Private backing List<T> exposed as IReadOnlyList<T> via AsReadOnly() for collection encapsulation"
    - "using DomainFile = Storage.Domain.Entities.File alias in consumer projects to avoid System.IO.File collision"

key-files:
  created:
    - backend/src/Storage.Domain/Common/EntityBase.cs
    - backend/src/Storage.Domain/Common/DomainException.cs
    - backend/src/Storage.Domain/Enums/FileStatus.cs
    - backend/src/Storage.Domain/Enums/Visibility.cs
    - backend/src/Storage.Domain/Enums/Permission.cs
    - backend/src/Storage.Domain/Events/DomainEvent.cs
    - backend/src/Storage.Domain/Events/FileCreatedEvent.cs
    - backend/src/Storage.Domain/Events/FileUploadedEvent.cs
    - backend/src/Storage.Domain/Events/FileScannedEvent.cs
    - backend/src/Storage.Domain/Events/FileReadyEvent.cs
    - backend/src/Storage.Domain/Events/FileDeletedEvent.cs
    - backend/src/Storage.Domain/Events/FilePermissionChangedEvent.cs
    - backend/src/Storage.Domain/ValueObjects/StorageKey.cs
    - backend/src/Storage.Domain/ValueObjects/Checksum.cs
    - backend/src/Storage.Domain/Entities/File.cs
    - backend/src/Storage.Domain/Entities/FileCategory.cs
    - backend/src/Storage.Domain/Entities/FileVersion.cs
    - backend/src/Storage.Domain/Entities/FilePermission.cs
    - backend/src/Storage.Domain/Entities/FileTag.cs
    - backend/src/Storage.Domain/Entities/AuditEntry.cs
  modified:
    - backend/tests/Storage.Domain.Tests/FileStatusTransitionTests.cs
    - backend/tests/Storage.Domain.Tests/StorageKeyTests.cs
    - backend/tests/Storage.Domain.Tests/ChecksumTests.cs
    - backend/tests/Storage.Domain.Tests/FileCategoryValidationTests.cs
    - backend/tests/Storage.Domain.Tests/DomainEventTests.cs
    - backend/tests/Storage.Domain.Tests/FileCollectionsTests.cs

key-decisions:
  - "FileCategory.Validate() returns (bool IsValid, string? Error) tuple — intentionally does NOT throw, unlike File.Transition()"
  - "File aggregate's collections use private List<T> backing fields exposed via AsReadOnly() as IReadOnlyList<T>"
  - "DomainEvent is an abstract record with positional EventId + OccurredAt — no auto-generation; callers pass Guid.NewGuid() and DateTime.UtcNow"
  - "FileCreatedEvent is raised in File.Create() factory; no events wired into Transition() per minimality principle (tests don't assert it)"
  - "Storage.Domain.csproj has zero PackageReferences — pure .NET BCL only, per architecture constraint"
  - "Exactly 6 event types; FilePreviewReadyEvent excluded per RESEARCH Pitfall 5 (Phase 1 out-of-scope)"

patterns-established:
  - "Result pattern: domain validation returns (bool, string?) rather than throwing"
  - "Exception pattern: state machine violations throw typed DomainException subclasses"
  - "Factory pattern: static File.Create() factory raises FileCreatedEvent and sets all invariants"
  - "Collection encapsulation: private readonly List<T> + IReadOnlyList<T> AsReadOnly() on all aggregate collections"
  - "Alias pattern: using DomainFile = Storage.Domain.Entities.File in all consumer files"

requirements-completed: [DOMAIN-01, DOMAIN-02, DOMAIN-03, DOMAIN-04, DOMAIN-05, DOMAIN-06]

# Metrics
duration: 4min
completed: 2026-05-16
---

# Phase 1 Plan 02: Domain Core Summary

**File aggregate with validated 5-state status machine, StorageKey/Checksum value objects, 6 domain events, and FileCategory policy validation — all pure BCL, 44 tests green**

## Performance

- **Duration:** ~4 min
- **Started:** 2026-05-15T21:51:42Z
- **Completed:** 2026-05-15T21:56:28Z
- **Tasks:** 2 (RED commit + GREEN commit)
- **Files modified:** 26

## Accomplishments

- Implemented all domain types in `Storage.Domain` following TDD (RED then GREEN)
- File aggregate with `Transition()` state machine (switch expression + tuple pattern) enforcing the 4 valid status transitions
- StorageKey value object with compiled regex enforcing `<tenantGuid>/<yyyy>/<mm>/<dd>/<fileGuid>` format; Checksum with SHA-256 hex validation and lowercase normalisation
- FileCategory with `Validate()` returning result tuple (not throwing) — intentionally different from `File.Transition()` which throws
- Exactly 6 domain event records, zero infrastructure dependencies in `Storage.Domain.csproj`

## Task Commits

1. **RED: Failing tests for domain core** - `6cdd1ef` (test)
2. **GREEN: Production domain implementation** - `69a9f99` (feat)

## Files Created/Modified

- `backend/src/Storage.Domain/Common/EntityBase.cs` - Abstract base with Guid Id, RaiseDomainEvent, ClearDomainEvents
- `backend/src/Storage.Domain/Common/DomainException.cs` - Abstract base + InvalidStatusTransitionException + InvalidStorageKeyException + InvalidChecksumException
- `backend/src/Storage.Domain/Enums/FileStatus.cs` - Pending/Scanning/Ready/Quarantined/Deleted
- `backend/src/Storage.Domain/Enums/Visibility.cs` - Private/Internal/Public
- `backend/src/Storage.Domain/Enums/Permission.cs` - Read/Write/Delete
- `backend/src/Storage.Domain/Events/DomainEvent.cs` - Abstract record base with EventId + OccurredAt
- `backend/src/Storage.Domain/Events/File*.cs` - 6 concrete event records (Created/Uploaded/Scanned/Ready/Deleted/PermissionChanged)
- `backend/src/Storage.Domain/ValueObjects/StorageKey.cs` - Immutable sealed class with compiled regex validation and Create() factory
- `backend/src/Storage.Domain/ValueObjects/Checksum.cs` - Immutable sealed class with SHA-256 hex validation and lowercase normalisation
- `backend/src/Storage.Domain/Entities/File.cs` - Aggregate root with Transition() state machine and IReadOnlyList collections
- `backend/src/Storage.Domain/Entities/FileCategory.cs` - Policy entity with Validate() returning (bool, string?) result tuple
- `backend/src/Storage.Domain/Entities/FileVersion.cs` - FileVersion entity with StorageKey, Checksum, SizeBytes
- `backend/src/Storage.Domain/Entities/FilePermission.cs` - Per-file ACL entity with PrincipalType, PrincipalId, Permission
- `backend/src/Storage.Domain/Entities/FileTag.cs` - Key/Value tag entity
- `backend/src/Storage.Domain/Entities/AuditEntry.cs` - Append-only audit record entity
- `backend/tests/Storage.Domain.Tests/*.cs` - All 6 test files populated with 44 tests

## Decisions Made

- `FileCategory.Validate()` returns `(bool IsValid, string? Error)` tuple — intentionally does NOT throw. `File.Transition()` throws `InvalidStatusTransitionException`. Different patterns, both intentional per plan spec.
- `DomainEvent` is an abstract positional record — callers supply `Guid.NewGuid()` and `DateTime.UtcNow` explicitly; no auto-generation to keep domain pure.
- `FileCreatedEvent` raised only in `File.Create()`. No events wired into `Transition()` since no test asserts it and minimality is preferred until a test demands it.
- `Storage.Domain.csproj` has zero `<PackageReference>` entries — pure .NET BCL only.
- Exactly 6 event types; `FilePreviewReadyEvent` excluded per RESEARCH Pitfall 5.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed xUnit.v3 analyzer error for unused Theory parameters**
- **Found during:** GREEN phase build
- **Issue:** xUnit.v3 treats unused Theory parameters as compile-time errors (xUnit1026). Original test signatures included `FileStatus from` in InlineData for readability but the parameter wasn't used in the body.
- **Fix:** Removed the `from` parameter from affected Theory method signatures (`Transition_FromPendingToScanning_Succeeds`, `Transition_FromScanningToReadyOrQuarantined_Succeeds`, `Transition_InvalidFromPending_ThrowsInvalidStatusTransitionException`) — tests are still semantically correct.
- **Files modified:** `backend/tests/Storage.Domain.Tests/FileStatusTransitionTests.cs`
- **Verification:** Build succeeds, all 44 tests pass
- **Committed in:** `69a9f99` (GREEN commit, staged together)

---

**Total deviations:** 1 auto-fixed (Rule 1 - Bug in test code)
**Impact on plan:** Minimal — test intent preserved, xUnit.v3 strict analyzer satisfied. No scope creep.

## Issues Encountered

None beyond the xUnit.v3 Theory parameter analyzer error (handled as Rule 1 auto-fix above).

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- `Storage.Domain` is complete and test-verified. Ready for Phase 2 (Application Layer & Port Interfaces).
- Phase 2 will define `IFileStorageProvider`, `ICacheProvider`, `IEventBus`, `IUnitOfWork` port interfaces in `Storage.Application`, building on these domain types.
- No blockers. `Storage.Application` can reference `Storage.Domain` freely; infrastructure projects must not reference `Storage.Domain` directly (only through `Storage.Application`).

---

*Phase: 01-solution-scaffold-domain-model*
*Completed: 2026-05-16*
