# Phase 2: Application Layer & Port Interfaces - Research

**Researched:** 2026-05-16
**Domain:** .NET 10 Application Layer — Ports and Adapters, Use-Case Services, Idempotency, Result Patterns
**Confidence:** HIGH

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| APP-01 | `IFileStorageProvider`, `ICacheProvider`, `IEventBus`, `IUnitOfWork` port interfaces defined in `Storage.Application/Abstractions/`; no infrastructure assembly referenced at compile time | Interface contracts extracted verbatim from §10.3; repository sub-ports confirmed from §10.3 IUnitOfWork definition |
| APP-02 | `UploadService.InitiateUploadAsync` validates against `FileCategory` policy and returns structured response | Validation flow from §6.1; response shape from §6.4 step 6; `FileCategory.Validate()` tuple pattern already in domain |
| APP-03 | `UploadService.InitiateUploadAsync` enforces idempotency via `Idempotency-Key`; same key+payload → same FileId; same key+different payload → 422 | `ICacheProvider.IsDistributed` guard for distributed idempotency; SQL fallback pattern confirmed from §10.3/CLAUDE.md |
| APP-04 | `UploadService.CompleteUploadAsync` verifies SHA-256 checksum, transitions status to `scanning`, publishes `file.uploaded` | Domain `Checksum` value object handles verification; `File.Transition(Scanning)` throws on invalid; `IEventBus.PublishAsync` carries `IntegrationEvent` |
| APP-05 | `DownloadService.GetFileAsync` authorises caller, reads metadata cache-first, returns pre-signed URL for ready files | Cache-first pattern documented in §6.5; `ICacheProvider` + `IUnitOfWork` ports; `StorageCapabilities.SupportsPresignedDownloadUrls` governs URL generation |
| APP-06 | `DownloadService.GetFileStreamAsync` proxies bytes for audited download path | `IFileStorageProvider.OpenReadStreamAsync` with `RangeHeader?`; proxy path only when `proxyRequired=true` |
| APP-07 | `FileManagementService` handles metadata patch, soft delete, hard delete, version creation, listing with cursor-based pagination, share link generation | Domain state machine `Ready → Deleted`; `IUnitOfWork.FileVersions` + `IAuditRepository`; pagination via `IFileRepository` query methods |
</phase_requirements>

---

## Summary

Phase 2 builds the interior of the hexagon: the four primary port interfaces in `Storage.Application/Abstractions/`, the repository sub-ports exposed by `IUnitOfWork`, three use-case service classes (`UploadService`, `DownloadService`, `FileManagementService`), and the application-layer DTOs and result types that map from domain objects to API-consumable shapes. Nothing in this phase touches SQL Server, Redis, RabbitMQ, or any object store — every outbound call goes through an interface; tests use NSubstitute mocks.

The most important design choices in this phase are: (1) how domain events and integration events relate, (2) where idempotency state is stored when `ICacheProvider.IsDistributed` is false, and (3) what result type the services return so Phase 6 can map errors to correct HTTP status codes without the application layer knowing about HTTP. The architecture document (§10.3) is unusually precise about interface signatures; the planner should treat those signatures as locked.

The existing `FileCategory.Validate()` returning `(bool, string?)` and `File.Transition()` throwing are the established precedents for the dual error strategy: values for policy failures, exceptions for invariant violations. Phase 2 must be consistent with this split across all three services.

**Primary recommendation:** Define port interfaces exactly as §10.3 specifies; define integration events as a separate record hierarchy in `Storage.Application/` that maps from domain events; return `Result<T, ApplicationError>` sealed-record discriminated union from all service methods; introduce `TimeProvider` (built-in .NET 8+) for testable timestamps.

---

## Standard Stack

### Core (Application Layer — no new NuGet packages required)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET 10 BCL | net10.0 | `TimeProvider`, `CancellationToken`, `Stream`, `RangeHeader` | Zero-dependency application core; BCL types only |
| Storage.Domain | (project ref) | `File`, `FileCategory`, `StorageKey`, `Checksum`, domain events | Already built in Phase 1 |

`Storage.Application.csproj` currently has **only** a project reference to `Storage.Domain` and no `PackageReference` entries. This is correct and must remain true. The application layer must be compilable with zero infrastructure NuGet packages.

### Supporting (Test Project)

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| xUnit v3 | 3.2.* | Test runner (matches domain tests) | All unit tests |
| FluentAssertions | 8.9.* | Assertion DSL (matches domain tests) | All assertions |
| NSubstitute | 5.x | Mock generation for port interfaces | Mocking `IFileStorageProvider`, `ICacheProvider`, `IEventBus`, `IUnitOfWork` in service tests |
| coverlet.collector | 6.0.4 | Coverage measurement (matches domain tests) | CI coverage gate |
| Microsoft.NET.Test.Sdk | 17.14.1 | Test SDK (matches domain tests) | Required for dotnet test |

**Installation (test project only):**
```bash
dotnet add backend/tests/Storage.Application.Tests package NSubstitute --version 5.*
dotnet add backend/tests/Storage.Application.Tests package xunit.v3 --version 3.2.*
dotnet add backend/tests/Storage.Application.Tests package FluentAssertions --version 8.9.*
dotnet add backend/tests/Storage.Application.Tests package coverlet.collector --version 6.0.4
dotnet add backend/tests/Storage.Application.Tests package Microsoft.NET.Test.Sdk --version 17.14.1
```

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| NSubstitute | Moq | Architecture doc (§15.2) says "NSubstitute preferred for readability" — NSubstitute is locked |
| `Result<T, ApplicationError>` sealed records | OneOf library | OneOf adds NuGet dependency to the application layer, violating zero-infra-dependency rule; BCL sealed records are zero-cost |
| `TimeProvider` (BCL) | `ISystemClock` custom port | `TimeProvider` is built into .NET 8+ and is already the standard .NET testing abstraction; no custom port needed |

---

## Architecture Patterns

### Recommended Project Structure

```
backend/
├── src/
│   └── Storage.Application/
│       ├── Abstractions/               # Port interfaces (APP-01)
│       │   ├── IFileStorageProvider.cs
│       │   ├── ICacheProvider.cs
│       │   ├── IEventBus.cs
│       │   ├── IUnitOfWork.cs
│       │   ├── IFileRepository.cs      # sub-port under IUnitOfWork
│       │   ├── IFileVersionRepository.cs
│       │   ├── IPermissionRepository.cs
│       │   └── IAuditRepository.cs
│       ├── Services/                   # Use-case services
│       │   ├── UploadService.cs        # APP-02, APP-03, APP-04
│       │   ├── DownloadService.cs      # APP-05, APP-06
│       │   └── FileManagementService.cs # APP-07
│       ├── DTOs/                       # Request/Response shapes
│       │   ├── InitiateUploadRequest.cs
│       │   ├── InitiateUploadResponse.cs
│       │   ├── CompleteUploadRequest.cs
│       │   ├── GetFileResponse.cs
│       │   └── FileListResponse.cs
│       ├── Events/                     # Integration events (separate from domain events)
│       │   ├── IntegrationEvent.cs     # base record
│       │   ├── FileCreatedIntegrationEvent.cs
│       │   ├── FileUploadedIntegrationEvent.cs
│       │   ├── FileReadyIntegrationEvent.cs
│       │   └── ...
│       └── Common/
│           ├── Result.cs               # Result<T, ApplicationError> discriminated union
│           └── ApplicationError.cs     # Sealed record hierarchy for error types
└── tests/
    └── Storage.Application.Tests/     # Wave 0 gap — must be created
        ├── Storage.Application.Tests.csproj
        ├── UploadServiceTests.cs
        ├── DownloadServiceTests.cs
        └── FileManagementServiceTests.cs
```

### Pattern 1: Port Interface Contract (from §10.3 — HIGH confidence)

**What:** Interfaces defined in `Storage.Application/Abstractions/` with exact signatures from architecture §10.3.
**When to use:** Any time the application layer needs to interact with storage, cache, messaging, or persistence.

```csharp
// Source: architecture §10.3
public interface IFileStorageProvider
{
    StorageCapabilities Capabilities { get; }

    Task<StoragePutResult> GetUploadTargetAsync(
        StoragePutRequest request, CancellationToken ct);

    Task<StorageGetResult> GetDownloadTargetAsync(
        string bucket, string key, TimeSpan ttl, CancellationToken ct);

    Task<Stream> OpenReadStreamAsync(
        string bucket, string key, RangeHeader? range, CancellationToken ct);

    Task WriteStreamAsync(
        string bucket, string key, Stream content,
        string contentType, CancellationToken ct);

    Task DeleteAsync(string bucket, string key, CancellationToken ct);
    Task<bool> ExistsAsync(string bucket, string key, CancellationToken ct);
}

public sealed record StorageCapabilities(
    bool SupportsPresignedUploadUrls,
    bool SupportsPresignedDownloadUrls,
    bool SupportsMultipartUpload,
    bool SupportsVersioning,
    bool SupportsServerSideEncryption,
    long MaxObjectSizeBytes);

public sealed record StoragePutResult(
    string? PresignedUrl,
    Dictionary<string, string>? Headers,
    bool ProxyRequired);
```

```csharp
// Source: architecture §10.3
public interface ICacheProvider
{
    bool IsDistributed { get; }

    Task<T?> GetAsync<T>(string key, CancellationToken ct);
    Task SetAsync<T>(string key, T value, TimeSpan? ttl, CancellationToken ct);
    Task RemoveAsync(string key, CancellationToken ct);
    Task<bool> ExistsAsync(string key, CancellationToken ct);
}
```

```csharp
// Source: architecture §10.3
// NOTE: IEventBus publishes IntegrationEvent (defined in Application layer),
//       NOT DomainEvent (defined in Domain layer). See Pattern 4 below.
public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct)
        where TEvent : IntegrationEvent;

    Task SubscribeAsync<TEvent, THandler>(CancellationToken ct)
        where TEvent : IntegrationEvent
        where THandler : IEventHandler<TEvent>;
}
```

```csharp
// Source: architecture §10.3
public interface IUnitOfWork
{
    IFileRepository         Files        { get; }
    IFileVersionRepository  FileVersions { get; }
    IPermissionRepository   Permissions  { get; }
    IAuditRepository        Audit        { get; }

    Task<int> SaveChangesAsync(CancellationToken ct);
    Task ExecuteInTransactionAsync(Func<Task> work, CancellationToken ct);
}
```

### Pattern 2: Result Type for Application Errors (HIGH confidence — from domain precedent)

**What:** Services return `Result<T, ApplicationError>` sealed-record union. No HTTP concepts in the application layer; Phase 6 maps error kinds to status codes.
**When to use:** Every public service method that can produce a policy violation or access-denied failure.

Precedent: `FileCategory.Validate()` returns `(bool, string?)` — policy failures are values. `File.Transition()` throws `InvalidStatusTransitionException` — invariant violations are exceptions. The application layer continues this split.

```csharp
// Source: established domain pattern, extended to application layer
public abstract record ApplicationError(string Message);

// Policy violations (return these as values, not exceptions)
public sealed record NotFoundError(string Message) : ApplicationError(Message);
public sealed record AccessDeniedError(string Message) : ApplicationError(Message);
public sealed record PolicyViolationError(string Message, int HttpStatusHint)
    : ApplicationError(Message);
    // HttpStatusHint: 400=BadRequest, 403=Forbidden, 413=TooLarge,
    //                  415=UnsupportedMedia, 422=UnprocessableEntity

public sealed record IdempotencyConflictError(string Message)
    : ApplicationError(Message);     // maps to 422 in Phase 6

// Generic result wrapper
public sealed class Result<T>
{
    public T? Value { get; }
    public ApplicationError? Error { get; }
    public bool IsSuccess => Error is null;

    private Result(T value)   { Value = value; }
    private Result(ApplicationError err) { Error = err; }

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(ApplicationError error) => new(error);
}
```

### Pattern 3: Idempotency Guard (MEDIUM confidence — from §10.3 + CLAUDE.md IsDistributed guard)

**What:** Idempotency keys cached under `idempotency:{key}` with TTL. When `ICacheProvider.IsDistributed` is false, fall back to SQL unique constraint on an `IdempotencyKeys` table (or an in-application dictionary for unit tests via the mock).
**When to use:** `UploadService.InitiateUploadAsync` only (APP-03).

```csharp
// Pseudocode — exact implementation is the planner's task
public async Task<Result<InitiateUploadResponse>> InitiateUploadAsync(
    InitiateUploadRequest req, CancellationToken ct)
{
    // Idempotency: check cache (or SQL) first
    var cacheKey = $"idempotency:{req.IdempotencyKey}";
    var cached = await _cache.GetAsync<IdempotencyRecord>(cacheKey, ct);
    if (cached is not null)
    {
        if (cached.PayloadHash != req.ComputeHash())
            return Result<InitiateUploadResponse>.Failure(
                new IdempotencyConflictError("Same key, different payload."));
        return Result<InitiateUploadResponse>.Success(cached.Response);
    }

    // ... validation and file creation ...

    // Store idempotency record
    var record = new IdempotencyRecord(req.ComputeHash(), response);
    await _cache.SetAsync(cacheKey, record, TimeSpan.FromHours(24), ct);

    return Result<InitiateUploadResponse>.Success(response);
}
```

**IsDistributed fallback:** When `_cache.IsDistributed` is false (unit tests / in-memory), the idempotency store is the in-memory cache mock, which is sufficient for testing. The architecture note is a reminder that distributed deployments must use the Redis adapter, not that Phase 2 itself must implement an alternate path — the port abstraction handles it transparently.

### Pattern 4: Domain Events vs. Integration Events (HIGH confidence — §10.3)

**What:** The domain layer publishes `DomainEvent` records (raised on `EntityBase`, cleared after persistence). The application layer dispatches `IntegrationEvent` records over `IEventBus` after successful persistence. These are two distinct hierarchies.

```
DomainEvent (Storage.Domain.Events)
  ├── FileCreatedEvent(FileId, TenantId, OwnerService, CategoryId, EventId, OccurredAt)
  ├── FileUploadedEvent(FileId, TenantId, OwnerService, EventId, OccurredAt)
  └── ...

IntegrationEvent (Storage.Application.Events) — what IEventBus carries
  ├── abstract record IntegrationEvent(Guid EventId, DateTime OccurredAt, string Source)
  ├── FileUploadedIntegrationEvent(FileId, TenantId, OwnerService, MimeType,
  │       SizeBytes, Tags, EventId, OccurredAt, Source)
  └── ...
```

**Why two hierarchies?** Integration events are the public contract with consumer microservices. They include richer routing metadata (`Source`, `Tags`, `MimeType`, `SizeBytes`) that downstream handlers need but that the domain shouldn't care about. The mapping from domain event → integration event happens in the application service, after `SaveChangesAsync` succeeds.

```csharp
// Source: architecture §10.3 (IntegrationEvent shape)
// After SaveChangesAsync, map domain event to integration event and publish:
foreach (var domainEvent in file.DomainEvents)
{
    if (domainEvent is FileUploadedEvent e)
    {
        await _eventBus.PublishAsync(new FileUploadedIntegrationEvent(
            FileId:      e.FileId,
            TenantId:    e.TenantId,
            OwnerService: e.OwnerService,
            MimeType:    file.MimeType,
            SizeBytes:   file.SizeBytes,
            Tags:        file.Tags.ToDictionary(t => t.Key, t => t.Value),
            EventId:     Guid.NewGuid(),
            OccurredAt:  DateTime.UtcNow,
            Source:      "storage-service"), ct);
    }
}
file.ClearDomainEvents();
```

### Pattern 5: StorageCapabilities Branch Point (HIGH confidence — CLAUDE.md)

**What:** The application layer branches exactly once on `Capabilities.SupportsPresignedUploadUrls`. All other paths are shared.
**When to use:** `UploadService.InitiateUploadAsync` when deciding whether to return a presigned URL or set `proxyRequired=true`.

```csharp
// Source: CLAUDE.md "StorageCapabilities is the branching point"
var putResult = await _storage.GetUploadTargetAsync(putRequest, ct);

return Result<InitiateUploadResponse>.Success(new InitiateUploadResponse(
    FileId:           file.Id,
    UploadUrl:        putResult.PresignedUrl,      // null when ProxyRequired
    UploadHeaders:    putResult.Headers ?? [],
    ExpiresAt:        _timeProvider.GetUtcNow().AddMinutes(15),
    ProxyRequired:    putResult.ProxyRequired,
    MultipartRequired: category.IsLargeFile
));
```

### Pattern 6: Caller Context as Explicit Parameter (HIGH confidence — testability requirement)

**What:** Service methods receive a `CallerContext` record (TenantId, PrincipalType, PrincipalId, Scopes) rather than injecting an `ITenantContext` service. Explicit parameter makes unit tests straightforward — no DI setup needed.

```csharp
public sealed record CallerContext(
    Guid TenantId,
    string PrincipalType,     // "service" | "user"
    string PrincipalId,
    IReadOnlyList<string> Scopes);
```

Phase 6 (API Layer) extracts `CallerContext` from the JWT claims and passes it into services.

### Pattern 7: TimeProvider for Testable Timestamps (HIGH confidence — .NET 8+ BCL)

**What:** Inject `TimeProvider` (built-in `System.TimeProvider`) into services so `expiresAt` calculations are deterministic in tests.
**When to use:** Any service that computes `DateTime.UtcNow` for presigned URL TTL, idempotency TTL, or `CreatedAt`.

```csharp
// Constructor injection — no custom interface needed
public class UploadService(
    IFileStorageProvider storage,
    ICacheProvider cache,
    IEventBus eventBus,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{ ... }

// In tests:
var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
var sut = new UploadService(..., fakeTime);
```

### Anti-Patterns to Avoid

- **Referencing any `Storage.Infrastructure.*` project from `Storage.Application`:** The `.csproj` must have zero references to infrastructure assemblies. Confirmed: current `Storage.Application.csproj` has only `<ProjectReference>` to `Storage.Domain`.
- **Using `System.IO.File` in Application-layer files that also use the domain `File` entity:** The domain `File` class collides with `System.IO.File`. Use the alias `using DomainFile = Storage.Domain.Entities.File;` as the domain test project already does.
- **Throwing exceptions for policy violations:** `FileCategory.Validate()` returns a tuple; this precedent means `UploadService` should return `Result<T, ApplicationError>` for size/MIME/extension failures, not throw.
- **Throwing exceptions for domain-level state machine violations:** `File.Transition()` throws `InvalidStatusTransitionException` — this IS correct and must not be changed.
- **Emitting DomainEvent records directly over IEventBus:** `IEventBus` accepts `IntegrationEvent`, not `DomainEvent`. Always map at the application layer boundary.
- **Storing HTTP status codes in the application layer:** `ApplicationError.HttpStatusHint` is a convenience int on `PolicyViolationError` only; errors must be mappable without coupling to `Microsoft.AspNetCore.Http`.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Storage key format | Custom string formatter | `StorageKey.Create(tenantId, date, fileId)` | Already built in Phase 1; enforce the `<tenantId>/<yyyy>/<mm>/<dd>/<uuid>` format |
| Checksum validation | SHA-256 regex in service | `new Checksum(value)` constructor throws on invalid | Already built in Phase 1; normalises casing |
| Mock objects | Hand-written test doubles | NSubstitute: `Substitute.For<IFileStorageProvider>()` | Interface-based mocking is safer; architecture §15.2 specifies NSubstitute |
| Timestamp generation | `DateTime.UtcNow` in service body | `_timeProvider.GetUtcNow()` | BCL TimeProvider makes tests deterministic |
| Idempotency key storage | Custom dictionary | `ICacheProvider` (or SQL via `IUnitOfWork`) with `idempotency:{key}` cache key | Port abstraction handles distributed vs. local transparently |
| Integration event publishing | Direct broker client | `IEventBus.PublishAsync<TEvent>()` | Port abstraction; concrete MassTransit wiring is Phase 5 |
| Domain state machine transitions | Status comparisons in services | `file.Transition(FileStatus.Scanning)` — throws on invalid | Invariants live in the aggregate; services never set `_status` directly |

**Key insight:** The domain layer (Phase 1) already provides value objects, a state machine, and the base entity pattern. The application layer's job is orchestration — it calls domain methods and ports, it does not re-implement domain logic.

---

## Common Pitfalls

### Pitfall 1: DomainEvent vs. IntegrationEvent Confusion

**What goes wrong:** Service publishes `FileUploadedEvent` (from `Storage.Domain.Events`) directly to `IEventBus`, which expects `IntegrationEvent` (from `Storage.Application.Events`). Code does not compile.
**Why it happens:** Both types are named similarly; the domain events were defined first.
**How to avoid:** Define `IntegrationEvent` base record in `Storage.Application.Events`. Add the mapping step in the service after `SaveChangesAsync`. Clear domain events with `file.ClearDomainEvents()` after mapping.
**Warning signs:** Any `using Storage.Domain.Events;` in a service that also calls `IEventBus.PublishAsync`.

### Pitfall 2: FileCategory.AllowedOwnerServices Not Validated

**What goes wrong:** `UploadService.InitiateUploadAsync` validates size, MIME, and extension via `category.Validate(file)` — but `AllowedOwnerServices` is NOT checked by `FileCategory.Validate()` (the domain method only checks the three file properties). The OwnerService check is the caller's responsibility.
**Why it happens:** `FileCategory.Validate(File file)` only has access to the `File` entity; `OwnerService` validation requires the caller's JWT claim, which is not on the file.
**How to avoid:** Explicitly check `category.AllowedOwnerServices.Contains(caller.PrincipalId)` in `UploadService.InitiateUploadAsync` before calling `category.Validate()`. Return `AccessDeniedError` if not in the list.
**Warning signs:** Tests pass for size/MIME/extension but no test covers "caller's service is not in AllowedOwnerServices".

### Pitfall 3: Serving Files Before Status = Ready

**What goes wrong:** `DownloadService.GetFileAsync` returns a presigned URL for a file with `Status = Scanning` or `Status = Pending`. Clients receive a URL that may not point to complete bytes yet.
**Why it happens:** Status check is forgotten or placed after URL generation.
**How to avoid:** Check `file.Status == FileStatus.Ready` before generating the download URL. Return `NotFoundError` for any non-Ready status (this is intentional — the architecture says "returns 404 for pending/scanning/cross-tenant").
**Warning signs:** Unit test for `GetFileAsync` does not include a case with `Status = Scanning`.

### Pitfall 4: Cross-Tenant Data Leak in GetFileAsync

**What goes wrong:** `DownloadService.GetFileAsync` loads the file by ID and returns it to any authenticated caller, regardless of tenant.
**Why it happens:** Query fetches by `fileId` only; tenant scoping is omitted.
**How to avoid:** All repository queries in `IFileRepository` must include `TenantId` from `CallerContext`. The design from §6.5 step 2: "reads metadata (Redis first, SQL Server on miss), **ensures Status=ready**". Return `NotFoundError` (not `AccessDeniedError`) for cross-tenant — this is intentional anti-enumeration.
**Warning signs:** Unit test `GetFile_CrossTenant_Returns404` is missing.

### Pitfall 5: Missing `file.ClearDomainEvents()` After Publishing

**What goes wrong:** Domain events accumulate on the entity; if the entity is ever persisted again (e.g., in a retry), the events are double-published.
**Why it happens:** `EntityBase.DomainEvents` is a mutable list; clearing is opt-in.
**How to avoid:** After `SaveChangesAsync` and after publishing all integration events, call `file.ClearDomainEvents()`.
**Warning signs:** Integration test sees duplicate `file.uploaded` events.

### Pitfall 6: IsLargeFile Not Forcing Multipart

**What goes wrong:** `InitiateUploadAsync` only sets `multipartRequired=true` when declared file size exceeds some threshold, but ignores `category.IsLargeFile`.
**Why it happens:** The per-size check is intuitive; the per-category flag is easy to miss.
**How to avoid:** From §6.4: "If `IsLargeFile=true`, force the multipart upload flow regardless of declared size." `multipartRequired = category.IsLargeFile || (size > category.MultipartThresholdBytes)`.
**Warning signs:** Test `InitiateUpload_LargeFileCategory_AlwaysSetsMultipartRequired` is missing or skipped.

---

## Code Examples

### Complete Interface Set (from §10.3)

```csharp
// Source: architecture §10.3 — IUnitOfWork with sub-repository ports
public interface IUnitOfWork
{
    IFileRepository         Files        { get; }
    IFileVersionRepository  FileVersions { get; }
    IPermissionRepository   Permissions  { get; }
    IAuditRepository        Audit        { get; }

    Task<int>  SaveChangesAsync(CancellationToken ct);
    Task       ExecuteInTransactionAsync(Func<Task> work, CancellationToken ct);
}

// Sub-ports (also in Storage.Application/Abstractions/)
public interface IFileRepository
{
    Task<DomainFile?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken ct);
    Task AddAsync(DomainFile file, CancellationToken ct);
    Task<IReadOnlyList<DomainFile>> ListAsync(FileListQuery query, CancellationToken ct);
}
// (IFileVersionRepository, IPermissionRepository, IAuditRepository similarly minimal)
```

### UploadService Skeleton (from §6.4 validation flow)

```csharp
// Source: architecture §6.4 validation flow + §10.3 service dependency pattern
public class UploadService(
    IFileStorageProvider storage,
    ICacheProvider cache,
    IEventBus eventBus,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Result<InitiateUploadResponse>> InitiateUploadAsync(
        InitiateUploadRequest req, CallerContext caller, CancellationToken ct)
    {
        // Step 1: idempotency check
        // Step 2: resolve FileCategory from IUnitOfWork
        // Step 3: check AllowedOwnerServices — return AccessDeniedError if not in list
        // Step 4: create file entity: DomainFile.Create(...)
        // Step 5: validate via category.Validate(file) — return PolicyViolationError if invalid
        // Step 6: check IsLargeFile for multipartRequired
        // Step 7: generate StorageKey via StorageKey.Create(tenantId, today, fileId)
        // Step 8: GetUploadTargetAsync — branches on Capabilities.SupportsPresignedUploadUrls
        // Step 9: save to IUnitOfWork, publish FileCreatedIntegrationEvent
        // Step 10: store idempotency record in cache
        // Return InitiateUploadResponse
    }

    public async Task<Result<CompleteUploadResponse>> CompleteUploadAsync(
        Guid fileId, CompleteUploadRequest req, CallerContext caller, CancellationToken ct)
    {
        // 1: load file (NotFoundError if not found or cross-tenant)
        // 2: verify Checksum: new Checksum(req.ChecksumSha256) — throws InvalidChecksumException
        //    if format bad; compare to stored checksum — PolicyViolationError on mismatch
        // 3: file.Transition(FileStatus.Scanning) — throws if invalid
        // 4: SaveChangesAsync
        // 5: publish FileUploadedIntegrationEvent
        // 6: file.ClearDomainEvents()
    }
}
```

### NSubstitute Test Pattern

```csharp
// Source: architecture §15.2 — NSubstitute mocking pattern
[Fact]
public async Task InitiateUpload_OversizeFile_ReturnsPolicyViolationError()
{
    // Arrange
    var storage  = Substitute.For<IFileStorageProvider>();
    var cache    = Substitute.For<ICacheProvider>();
    var eventBus = Substitute.For<IEventBus>();
    var uow      = Substitute.For<IUnitOfWork>();
    var timeProvider = TimeProvider.System;

    var category = new FileCategory { Id = "document", MaxSizeBytes = 1024 };
    uow.Files.GetCategoryByIdAsync("document", Arg.Any<CancellationToken>())
       .Returns(category);

    var sut = new UploadService(storage, cache, eventBus, uow, timeProvider);
    var req = new InitiateUploadRequest("document", "test.pdf", "application/pdf",
              SizeBytes: 2048, IdempotencyKey: Guid.NewGuid().ToString());
    var caller = new CallerContext(Guid.NewGuid(), "service", "documents-service", []);

    // Act
    var result = await sut.InitiateUploadAsync(req, caller, CancellationToken.None);

    // Assert
    result.IsSuccess.Should().BeFalse();
    result.Error.Should().BeOfType<PolicyViolationError>();
    ((PolicyViolationError)result.Error!).HttpStatusHint.Should().Be(413);
}
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `ISystemClock` custom interface | `System.TimeProvider` (BCL) | .NET 8 | No custom port needed for time; `TimeProvider.System` in prod, `FakeTimeProvider` in tests |
| `IOptions<T>` injected into every service | Options validated at startup via `ValidateOnStart()` + `ValidateDataAnnotations()` | .NET 6+ | Misconfigured adapters fail at startup, not at first use |
| Exception-only error propagation | `Result<T, TError>` or discriminated union | C# 9+ records | Clean discrimination in Phase 6 between policy violations and infrastructure failures |

**Deprecated/outdated:**
- `IActionResult`-returning services: Services must return domain/application types; HTTP concerns belong in Phase 6. Never return `IActionResult` or `Microsoft.AspNetCore.*` types from `Storage.Application`.

---

## Open Questions

1. **FileCategory as aggregate vs. read model**
   - What we know: `FileCategory` is already defined in `Storage.Domain.Entities` with `Validate(File)`. `IUnitOfWork.Files` is the repository; there is no explicit `IFileCategoryRepository` in §10.3.
   - What's unclear: Should `FileCategory` be fetched via a separate `ICategoryRepository` exposed by `IUnitOfWork`, or should it be included as a navigation on `IFileRepository.GetCategoryByIdAsync`?
   - Recommendation: Add `IFileCategoryRepository Categories { get; }` to `IUnitOfWork`. Simpler to test and keeps read concerns separate from write concerns. Phase 3 adds `EfFileCategoryRepository`.

2. **Idempotency storage when IsDistributed is false**
   - What we know: Architecture says "fall back to SQL Server when false". But Phase 2 has no SQL adapter yet.
   - What's unclear: Must Phase 2 define an `IIdempotencyStore` port, or is the `ICacheProvider` mock sufficient for unit tests and the real distributed logic deferred to Phase 5?
   - Recommendation: Unit tests mock `ICacheProvider`; no separate port needed. Document the production requirement: "idempotency keys MUST use a distributed cache adapter; `ICacheProvider.IsDistributed` must be checked in the service and logged as a warning when false." Phase 5 verifies this.

3. **Soft-delete vs. hard-delete in FileManagementService**
   - What we know: Soft delete: `file.Transition(FileStatus.Deleted)` + set `DeletedAt`. Hard delete (`?hard=true`): requires `storage.admin` scope in JWT — but scope enforcement in Phase 2 is done via `CallerContext.Scopes`.
   - What's unclear: Does Phase 2 implement the hard-delete physical object store removal? (`IFileStorageProvider.DeleteAsync`)
   - Recommendation: Yes — `FileManagementService.HardDeleteAsync` calls `IFileStorageProvider.DeleteAsync` and `IUnitOfWork.Files` permanent remove. The port is available; the adapter is mocked in tests.

4. **Share link generation algorithm**
   - What we know: APP-07 includes "share link generation". Architecture §3.7 mentions "tokenised share links with optional expiry and password." No concrete interface is specified.
   - What's unclear: Is the share link a presigned URL (handled by `IFileStorageProvider.GetDownloadTargetAsync`) or a custom server-side token stored in DB?
   - Recommendation: Implement as a signed token (e.g., HMAC-SHA256 of `fileId + expiresAt + tenantId`) stored in cache or DB, resolved via `GET /v1/files/{id}/share/{token}` in Phase 6. Introduce `IShareTokenService` port if needed; keep algorithm in application layer.

---

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit v3 3.2.* |
| Config file | None — see Wave 0 |
| Quick run command | `dotnet test backend/tests/Storage.Application.Tests -v minimal` |
| Full suite command | `dotnet test backend/tests/ --filter "FullyQualifiedName~Application" -v minimal` |

### Phase Requirements to Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| APP-01 | Compile-time: no infrastructure assembly referenced | Build check | `dotnet build backend/src/Storage.Application/` | ❌ Wave 0 |
| APP-02 | `InitiateUpload` valid request → structured response | unit | `dotnet test --filter "FullyQualifiedName~UploadServiceTests.InitiateUpload_ValidRequest"` | ❌ Wave 0 |
| APP-02 | `InitiateUpload` unknown category → error | unit | `dotnet test --filter "FullyQualifiedName~UploadServiceTests.InitiateUpload_UnknownCategory"` | ❌ Wave 0 |
| APP-02 | `InitiateUpload` size exceeded → PolicyViolationError(413) | unit | `dotnet test --filter "FullyQualifiedName~UploadServiceTests.InitiateUpload_OversizeFile"` | ❌ Wave 0 |
| APP-02 | `InitiateUpload` disallowed MIME → PolicyViolationError(415) | unit | `dotnet test --filter "FullyQualifiedName~UploadServiceTests.InitiateUpload_DisallowedMimeType"` | ❌ Wave 0 |
| APP-02 | `InitiateUpload` disallowed extension → PolicyViolationError(415) | unit | `dotnet test --filter "FullyQualifiedName~UploadServiceTests.InitiateUpload_DisallowedExtension"` | ❌ Wave 0 |
| APP-02 | `InitiateUpload` disallowed owner service → AccessDeniedError | unit | `dotnet test --filter "FullyQualifiedName~UploadServiceTests.InitiateUpload_ForbiddenOwnerService"` | ❌ Wave 0 |
| APP-02 | `InitiateUpload` IsLargeFile category → multipartRequired=true | unit | `dotnet test --filter "FullyQualifiedName~UploadServiceTests.InitiateUpload_LargeFileCategory_MultipartRequired"` | ❌ Wave 0 |
| APP-03 | Same key + same payload → same FileId | unit | `dotnet test --filter "FullyQualifiedName~UploadServiceTests.InitiateUpload_SameKeyAndPayload_ReturnsExistingFileId"` | ❌ Wave 0 |
| APP-03 | Same key + different payload → IdempotencyConflictError | unit | `dotnet test --filter "FullyQualifiedName~UploadServiceTests.InitiateUpload_SameKeyDifferentPayload_Returns422"` | ❌ Wave 0 |
| APP-04 | Valid checksum → status scanning + event published | unit | `dotnet test --filter "FullyQualifiedName~UploadServiceTests.CompleteUpload_ValidChecksum_TransitionsToScanning"` | ❌ Wave 0 |
| APP-04 | Checksum mismatch → error | unit | `dotnet test --filter "FullyQualifiedName~UploadServiceTests.CompleteUpload_ChecksumMismatch_ReturnsError"` | ❌ Wave 0 |
| APP-05 | Ready file + authorised caller → presigned URL | unit | `dotnet test --filter "FullyQualifiedName~DownloadServiceTests.GetFile_ReadyFile_ReturnsPresignedUrl"` | ❌ Wave 0 |
| APP-05 | Pending/scanning file → NotFoundError | unit | `dotnet test --filter "FullyQualifiedName~DownloadServiceTests.GetFile_PendingFile_ReturnsNotFound"` | ❌ Wave 0 |
| APP-05 | Cross-tenant file ID → NotFoundError | unit | `dotnet test --filter "FullyQualifiedName~DownloadServiceTests.GetFile_CrossTenant_ReturnsNotFound"` | ❌ Wave 0 |
| APP-05 | Insufficient permission → AccessDeniedError | unit | `dotnet test --filter "FullyQualifiedName~DownloadServiceTests.GetFile_InsufficientPermission_ReturnsAccessDenied"` | ❌ Wave 0 |
| APP-06 | ProxyRequired → OpenReadStreamAsync called | unit | `dotnet test --filter "FullyQualifiedName~DownloadServiceTests.GetFileStream_ProxyPath_CallsOpenReadStream"` | ❌ Wave 0 |
| APP-07 | Soft delete ready file → status deleted | unit | `dotnet test --filter "FullyQualifiedName~FileManagementServiceTests.SoftDelete_ReadyFile_SetsDeletedStatus"` | ❌ Wave 0 |
| APP-07 | Hard delete without admin scope → AccessDeniedError | unit | `dotnet test --filter "FullyQualifiedName~FileManagementServiceTests.HardDelete_WithoutAdminScope_ReturnsAccessDenied"` | ❌ Wave 0 |
| APP-07 | Pagination cursor returns correct page | unit | `dotnet test --filter "FullyQualifiedName~FileManagementServiceTests.ListFiles_WithCursor_ReturnsPaginatedResults"` | ❌ Wave 0 |

### Sampling Rate

- **Per task commit:** `dotnet test backend/tests/Storage.Application.Tests -v minimal`
- **Per wave merge:** `dotnet test backend/tests/ -v minimal`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps

- [ ] `backend/tests/Storage.Application.Tests/Storage.Application.Tests.csproj` — test project does not exist; create with NSubstitute + xUnit v3 + FluentAssertions + coverlet
- [ ] `backend/tests/Storage.Application.Tests/UploadServiceTests.cs` — covers APP-02, APP-03, APP-04
- [ ] `backend/tests/Storage.Application.Tests/DownloadServiceTests.cs` — covers APP-05, APP-06
- [ ] `backend/tests/Storage.Application.Tests/FileManagementServiceTests.cs` — covers APP-07
- [ ] Framework install: `dotnet new xunit -n Storage.Application.Tests -o backend/tests/Storage.Application.Tests --framework net10.0`
- [ ] Add to solution: `dotnet sln backend/StorageService.sln add backend/tests/Storage.Application.Tests/Storage.Application.Tests.csproj`

---

## Sources

### Primary (HIGH confidence)

- `storage-microservice-architecture.md` §10.3 — verbatim interface contracts for all four ports, `StorageCapabilities`, `StoragePutResult`, `IntegrationEvent` shape
- `storage-microservice-architecture.md` §6.1 — validation flow (7 steps); AllowedOwnerServices check; IsLargeFile → multipartRequired
- `storage-microservice-architecture.md` §6.4 — upload sequence (11 steps); response shape `{ fileId, uploadUrl, uploadHeaders, expiresAt, proxyRequired, multipartRequired }`
- `storage-microservice-architecture.md` §6.5 — download sequence; cache-first read; status=ready gate; cross-tenant = 404
- `storage-microservice-architecture.md` §15.2 — tooling: xUnit, FluentAssertions, NSubstitute
- `storage-microservice-architecture.md` §15.3 — 20 application-layer test cases (mapped to test plan above)
- `CLAUDE.md` — `StorageCapabilities` single branch point; `ICacheProvider.IsDistributed` guard; events carry no bytes
- `backend/src/Storage.Domain/Entities/File.cs` — `File.Transition()` throws, `File.Create()` factory, domain events raised
- `backend/src/Storage.Domain/Entities/FileCategory.cs` — `Validate()` returns `(bool, string?)` tuple; confirmed precedent
- `backend/src/Storage.Domain/Common/EntityBase.cs` — `DomainEvents`, `ClearDomainEvents()` pattern
- `.planning/STATE.md` — confirmed decisions: `FileCategory.Validate()` returns tuple (not throw); domain has 6 event types
- `backend/tests/Storage.Domain.Tests/Storage.Domain.Tests.csproj` — confirmed package versions: xUnit v3 3.2.*, FluentAssertions 8.9.*, coverlet 6.0.4

### Secondary (MEDIUM confidence)

- .NET 8+ `System.TimeProvider` — BCL class for testable time; `FakeTimeProvider` from `Microsoft.Extensions.TimeProvider.Testing` available for tests
- NSubstitute 5.x compatibility with .NET 10 — widely used; no known incompatibilities

### Tertiary (LOW confidence)

- Share link token algorithm — not specified in architecture doc; recommendation to use HMAC-SHA256 is based on common .NET practice, not a primary source

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all package versions confirmed from existing test project and architecture doc
- Architecture (port interfaces): HIGH — verbatim from §10.3, primary source
- Architecture (result type + caller context): MEDIUM — consistent with established domain pattern; specific record shapes are the planner's implementation detail
- Pitfalls: HIGH — derived from architecture intent + domain code inspection
- Test cases: HIGH — mapped directly from §15.3 architecture test list

**Research date:** 2026-05-16
**Valid until:** 2026-06-16 (stable .NET 10 and xUnit v3 APIs; architecture doc is local source-of-truth)

## RESEARCH COMPLETE
