# Phase 1: Solution Scaffold & Domain Model - Research

**Researched:** 2026-05-15
**Domain:** .NET 10 solution scaffolding, Hexagonal/Ports-and-Adapters architecture, DDD domain modelling, Docker Compose local dev stack
**Confidence:** HIGH (core stack verified; one item at MEDIUM — Ocelot .NET 10 official GA)

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- Namespace prefix: `Storage.*` — matches the architecture doc exactly
- Solution file: `backend/StorageService.sln`
- Projects: `Storage.Domain`, `Storage.Application`, `Storage.Infrastructure.Persistence.SqlServer`, `Storage.Infrastructure.Storage.Wasabi`, `Storage.Infrastructure.Storage.AzureBlob`, `Storage.Infrastructure.Storage.FileSystem`, `Storage.Infrastructure.Cache.Redis`, `Storage.Infrastructure.Cache.InMemory`, `Storage.Infrastructure.Messaging.RabbitMQ`, `Storage.Infrastructure.Messaging.AzureServiceBus`, `Storage.Sdk`, `Storage.Api`
- `File` and all domain entities are mutable `class` types — not records
- State transitions are encapsulated methods: `file.Transition(FileStatus.Scanning)` throws `DomainException` for invalid transitions
- Domain rule violations signal via `DomainException` (custom exception, not Result<T>)
- API layer catches `DomainException` and maps to 422 Unprocessable Entity
- Domain events are plain C# `record` types in `Storage.Domain` — no dispatcher wired in Phase 1
- Entities inherit from `abstract EntityBase` which holds `List<DomainEvent> _events`, exposes `RaiseDomainEvent()` and `ClearDomainEvents()`
- MassTransit wires up dispatching in Phase 5; nothing dispatches events until then
- Single `docker-compose.yml` at repo root starts all backend services: SQL Server, Redis, RabbitMQ, Keycloak, ClamAV, Storage API, Ocelot gateway
- Angular runs locally via `ng serve` — not in Docker Compose
- Developer workflow: `docker-compose up` for backend, `ng serve` for frontend

### Claude's Discretion

- Exact `EntityBase` generic typing (e.g., `EntityBase<TId>` vs `EntityBase`)
- `DomainException` hierarchy (single class or subclasses per rule)
- Angular workspace library project names within `frontend/`

### Deferred Ideas (OUT OF SCOPE)

- None — discussion stayed within phase scope
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| SETUP-01 | Backend .NET 10 solution in `backend/` with all projects from §10.2 | Solution scaffold pattern; `dotnet new` commands; project reference rules |
| SETUP-02 | Angular workspace in `frontend/` with shared library and demo app | Angular 22 CLI workspace generation with `--standalone` |
| SETUP-03 | Root `docker-compose.yml` brings up SQL Server, Redis, RabbitMQ, Keycloak, ClamAV, and the Storage API service | Pinned Docker image tags; service dependencies; health-check wiring |
| SETUP-04 | Ocelot gateway project routes `/storage/v1/*` → Storage API; JWT validation against Keycloak | Ocelot 24.1 config; note that `/api/documents/*` route will 502 until Phase 7 |
| SETUP-05 | Developer can run full local stack with `docker-compose up` and hit Ocelot on port 5000 | Compose port mapping; readiness probes; `depends_on` ordering |
| DOMAIN-01 | `File` entity implements status state machine; invalid transitions rejected | State machine pattern via `switch` expression + `DomainException` |
| DOMAIN-02 | `FileCategory` entity holds all policy fields from §6.1; exposes `Validate(file)` method | Category schema from §6.1; validation flow from §6.4 |
| DOMAIN-03 | `StorageKey` value object enforces `<tenantId>/<yyyy>/<mm>/<dd>/<uuid>` format | Regex validation; value object pattern |
| DOMAIN-04 | `Checksum` value object stores SHA-256 hex, normalises casing, rejects malformed values | Hex validation; `ToLowerInvariant()` normalisation |
| DOMAIN-05 | `FileVersion`, `FilePermission`, `FileTag`, `AuditEntry` entities exist as owned collections | Owned collection modelling; encapsulated lists on `File` |
| DOMAIN-06 | Six domain events defined in `Storage.Domain` (FileCreated, FileUploaded, FileScanned, FileReady, FileDeleted, FilePermissionChanged) | Abstract `DomainEvent` record base; event record pattern |
</phase_requirements>

---

## Summary

Phase 1 establishes the greenfield repository from zero. The work divides into three independent workstreams: (1) scaffold the `.NET 10` solution with twelve projects wired correctly, (2) implement the domain core — entities, value objects, events, and the state machine — entirely free of infrastructure dependencies, and (3) stand up the full Docker Compose local dev stack including an Ocelot gateway that proves the API container is reachable.

The domain model is the highest-value output of this phase. `File` is the aggregate root; its status transition logic is the first testable behaviour. `StorageKey` and `Checksum` are value objects whose format constraints act as invariant guards. Six domain events are defined as C# `record` types deriving from an abstract `DomainEvent` base; no dispatcher is wired until Phase 5.

The Docker Compose stack has a latent dependency tension: SETUP-04 asks for an Ocelot route to a Documents API that does not exist until Phase 7. The correct resolution is to define the `/api/documents/*` route in `ocelot.json` pointing at the eventual host/port, accepting that it returns 502 until Phase 7 delivers the service. This does not block the Phase 1 success criterion ("Ocelot gateway responds on port 5000").

**Primary recommendation:** Scaffold all twelve projects in one `dotnet` CLI sequence, wire project references immediately and verify with `dotnet build`, then implement domain types in `Storage.Domain` with matching xUnit tests in `backend/tests/Storage.Domain.Tests/` — success criteria 3, 4, and 5 are only verifiable with a test project.

---

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET SDK | 10.0.x (LTS, GA Nov 2025) | Runtime and build toolchain | Architecture-locked; LTS until Nov 2028 |
| C# | 14 (ships with .NET 10) | Language | Ships with .NET 10 SDK |
| ASP.NET Core Minimal APIs | 10.0.x | HTTP host for `Storage.Api` and Ocelot host | Architecture-locked |
| Ocelot | 24.1.0 | API Gateway / reverse proxy in demo stack | Architecture-locked; runs on .NET 10 via backward compatibility (net8.0 TFM on .NET 10 runtime); beta 25.0.0-beta.2 targets net10.0 explicitly |
| Angular CLI | 22.x (Angular 22 GA ~May 2026) | Frontend workspace scaffold | Architecture-locked "latest stable" |

### Supporting (test projects — Phase 1 only)

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| xunit.v3 | 3.2.x | Test framework | All test projects in this solution |
| xunit.runner.visualstudio | 3.1.5 | VS/CI test adapter | Required alongside xunit.v3 |
| FluentAssertions | 8.9.x | Assertion library | All test projects |
| NSubstitute | 5.3.x | Mocking library | Application-layer tests (Phase 2+); not needed for pure domain tests in Phase 1 |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Ocelot 24.1.0 (stable) | Ocelot 25.0.0-beta.2 | Beta ships with net10.0 TFM explicitly; stable runs fine on .NET 10 via backward compat. Use stable unless net10.0 TFM is required by CI tooling. |
| xUnit v3 | xUnit v2 | v3 has Microsoft Testing Platform support; v2 still works. Architecture docs do not specify v2 vs v3 — use v3. |
| mutable `class` entities | `record` entities | Architecture doc and CONTEXT locked mutable classes. |

### Installation

```bash
# Backend solution (run from repo root)
dotnet new sln -n StorageService -o backend --format sln

# Domain and application core (zero infrastructure dependencies)
dotnet new classlib -n Storage.Domain    -f net10.0 -o backend/src/Storage.Domain
dotnet new classlib -n Storage.Application -f net10.0 -o backend/src/Storage.Application

# Infrastructure projects
dotnet new classlib -n Storage.Infrastructure.Persistence.SqlServer -f net10.0 -o backend/src/Storage.Infrastructure.Persistence.SqlServer
dotnet new classlib -n Storage.Infrastructure.Storage.Wasabi        -f net10.0 -o backend/src/Storage.Infrastructure.Storage.Wasabi
dotnet new classlib -n Storage.Infrastructure.Storage.AzureBlob     -f net10.0 -o backend/src/Storage.Infrastructure.Storage.AzureBlob
dotnet new classlib -n Storage.Infrastructure.Storage.FileSystem     -f net10.0 -o backend/src/Storage.Infrastructure.Storage.FileSystem
dotnet new classlib -n Storage.Infrastructure.Cache.Redis            -f net10.0 -o backend/src/Storage.Infrastructure.Cache.Redis
dotnet new classlib -n Storage.Infrastructure.Cache.InMemory         -f net10.0 -o backend/src/Storage.Infrastructure.Cache.InMemory
dotnet new classlib -n Storage.Infrastructure.Messaging.RabbitMQ     -f net10.0 -o backend/src/Storage.Infrastructure.Messaging.RabbitMQ
dotnet new classlib -n Storage.Infrastructure.Messaging.AzureServiceBus -f net10.0 -o backend/src/Storage.Infrastructure.Messaging.AzureServiceBus
dotnet new classlib -n Storage.Sdk -f net10.0 -o backend/src/Storage.Sdk

# API host
dotnet new webapi  -n Storage.Api -f net10.0 -o backend/src/Storage.Api  # minimal APIs is the default in .NET 10; --use-minimal-apis flag was removed in .NET 7+

# Test project (needed in Phase 1 for success criteria verification)
dotnet new xunit   -n Storage.Domain.Tests -f net10.0 -o backend/tests/Storage.Domain.Tests

# Add all to solution
# NOTE: PowerShell does not expand ** globs; use bash or list each .csproj path explicitly.
# In bash: dotnet sln backend/StorageService.sln add backend/src/Storage.Domain/Storage.Domain.csproj ...
# (repeat for each project, or use: find backend/src -name "*.csproj" | xargs dotnet sln backend/StorageService.sln add)

# Angular workspace
ng new demo-app --directory frontend --standalone --routing --style scss
cd frontend
ng generate library shared-lib
```

---

## Architecture Patterns

### Recommended Project Structure

```
backend/
├── StorageService.sln
├── src/
│   ├── Storage.Domain/
│   │   ├── Entities/
│   │   │   ├── File.cs
│   │   │   ├── FileCategory.cs
│   │   │   ├── FileVersion.cs
│   │   │   ├── FilePermission.cs
│   │   │   ├── FileTag.cs
│   │   │   └── AuditEntry.cs
│   │   ├── ValueObjects/
│   │   │   ├── StorageKey.cs
│   │   │   └── Checksum.cs
│   │   ├── Enums/
│   │   │   ├── FileStatus.cs
│   │   │   ├── Visibility.cs
│   │   │   └── Permission.cs
│   │   ├── Events/
│   │   │   ├── DomainEvent.cs        (abstract record base)
│   │   │   ├── FileCreatedEvent.cs
│   │   │   ├── FileUploadedEvent.cs
│   │   │   ├── FileScannedEvent.cs
│   │   │   ├── FileReadyEvent.cs
│   │   │   ├── FileDeletedEvent.cs
│   │   │   └── FilePermissionChangedEvent.cs
│   │   └── Common/
│   │       └── EntityBase.cs
│   ├── Storage.Application/
│   │   └── Abstractions/             (stub folder — port interfaces added in Phase 2)
│   ├── Storage.Infrastructure.*/
│   ├── Storage.Sdk/
│   └── Storage.Api/
│       ├── Program.cs
│       ├── Dockerfile
│       └── ocelot.json               (gateway config if Ocelot is separate project)
├── tests/
│   └── Storage.Domain.Tests/
│       ├── FileStatusTransitionTests.cs
│       ├── StorageKeyTests.cs
│       └── ChecksumTests.cs
frontend/
├── projects/
│   ├── demo-app/
│   └── shared-lib/
docker-compose.yml
```

**Note on path:** Architecture §10.2 shows `src/Storage.Domain/...`. CONTEXT specifies `backend/StorageService.sln`. The canonical path is `backend/src/Storage.Domain/` and `backend/tests/Storage.Domain.Tests/`.

### Pattern 1: EntityBase with Domain Events

**What:** Abstract base class holding the internal domain event list. All domain entities extend it.
**When to use:** Every entity in `Storage.Domain`.

```csharp
// Storage.Domain/Common/EntityBase.cs
// Recommendation: non-generic EntityBase (Guid Id).
// Rationale: all entities in this design use Guid PKs; generic TId adds ceremony
// for zero benefit here. If strongly-typed IDs are added later, refactor then.
public abstract class EntityBase
{
    public Guid Id { get; protected set; }

    private readonly List<DomainEvent> _events = [];

    public IReadOnlyList<DomainEvent> DomainEvents => _events.AsReadOnly();

    protected void RaiseDomainEvent(DomainEvent @event) => _events.Add(@event);

    public void ClearDomainEvents() => _events.Clear();
}
```

### Pattern 2: Status State Machine via Encapsulated Transition Method

**What:** The `File` entity encapsulates all valid transitions in a single `Transition()` method. Invalid transitions throw `InvalidStatusTransitionException`.
**When to use:** Only on `File`; only this pattern is permitted for status changes.

```csharp
// Storage.Domain/Entities/File.cs (excerpt)
public void Transition(FileStatus newStatus)
{
    var valid = (_status, newStatus) switch
    {
        (FileStatus.Pending,    FileStatus.Scanning)    => true,
        (FileStatus.Scanning,   FileStatus.Ready)       => true,
        (FileStatus.Scanning,   FileStatus.Quarantined) => true,
        (FileStatus.Ready,      FileStatus.Deleted)     => true,
        _ => false
    };

    if (!valid)
        throw new InvalidStatusTransitionException(_status, newStatus);

    _status = newStatus;
    // raise appropriate domain event
}
```

### Pattern 3: Value Object — Immutable with Validation in Constructor

**What:** `StorageKey` and `Checksum` are immutable classes (not records — records have reference equality issues when wrapping strings). Constructor validates; factory method returns instance.
**When to use:** Enforce format invariants at construction time; never pass raw strings.

```csharp
// Storage.Domain/ValueObjects/StorageKey.cs
public sealed class StorageKey
{
    // <tenantId>/<yyyy>/<mm>/<dd>/<uuid>
    private static readonly Regex _pattern =
        new(@"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}"
           + @"/\d{4}/\d{2}/\d{2}"
           + @"/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string Value { get; }

    public StorageKey(string value)
    {
        if (!_pattern.IsMatch(value))
            throw new InvalidStorageKeyException(value);
        Value = value;
    }

    public static StorageKey Create(Guid tenantId, DateOnly date, Guid fileId) =>
        new($"{tenantId:D}/{date:yyyy}/{date:MM}/{date:dd}/{fileId:D}");
}

// Storage.Domain/ValueObjects/Checksum.cs
public sealed class Checksum
{
    private static readonly Regex _sha256Hex =
        new(@"^[0-9a-f]{64}$", RegexOptions.Compiled);

    public string Value { get; }

    public Checksum(string value)
    {
        var normalized = value?.ToLowerInvariant()
            ?? throw new ArgumentNullException(nameof(value));
        if (!_sha256Hex.IsMatch(normalized))
            throw new InvalidChecksumException(value);
        Value = normalized;
    }
}
```

### Pattern 4: Domain Event Records

**What:** Domain events are immutable `record` types inheriting from `abstract record DomainEvent`. No dispatcher in Phase 1 — events accumulate on the entity, dispatching wired in Phase 5.
**When to use:** All six events in DOMAIN-06.

```csharp
// Storage.Domain/Events/DomainEvent.cs
public abstract record DomainEvent(
    Guid EventId,
    DateTime OccurredAt);

// Storage.Domain/Events/FileCreatedEvent.cs
public sealed record FileCreatedEvent(
    Guid FileId,
    Guid TenantId,
    string OwnerService,
    string CategoryId,
    Guid EventId,
    DateTime OccurredAt) : DomainEvent(EventId, OccurredAt);
```

**Event count note:** Phase 1 (DOMAIN-06) defines exactly 6 events. Architecture §8.2 lists a 7th (`file.preview_ready`) — this is intentionally excluded from Phase 1 scope.

### Pattern 5: DomainException Hierarchy

**Recommendation (Claude's discretion):** Use a base `DomainException` plus named subclasses per rule. This gives test code type-safe `Assert.Throws<T>` granularity and richer HTTP mapping in Phase 6.

```csharp
// Storage.Domain/Common/DomainException.cs
public abstract class DomainException(string message) : Exception(message);

public sealed class InvalidStatusTransitionException(FileStatus from, FileStatus to)
    : DomainException($"Cannot transition from {from} to {to}.");

public sealed class InvalidStorageKeyException(string key)
    : DomainException($"'{key}' is not a valid storage key.");

public sealed class InvalidChecksumException(string value)
    : DomainException($"'{value}' is not a valid SHA-256 checksum.");
```

### Pattern 6: Ocelot Gateway

**What:** A minimal ASP.NET Core app hosting Ocelot. Reads `ocelot.json` for routing rules.
**Note on SETUP-04:** The Ocelot config MUST include the `/api/documents/*` route pointing to the Documents API host/port even though that service does not exist until Phase 7. This is correct — the route returns 502 until Phase 7. Do not omit the route.

```json
// ocelot.json (partial — Phase 1 wires storage route fully, documents route as placeholder)
{
  "Routes": [
    {
      "DownstreamPathTemplate": "/v1/{everything}",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [{ "Host": "storage-api", "Port": 8080 }],
      "UpstreamPathTemplate": "/storage/v1/{everything}",
      "AuthenticationOptions": {
        "AuthenticationProviderKey": "Bearer"
      }
    },
    {
      "DownstreamPathTemplate": "/api/documents/{everything}",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [{ "Host": "documents-api", "Port": 8080 }],
      "UpstreamPathTemplate": "/api/documents/{everything}",
      "AuthenticationOptions": {
        "AuthenticationProviderKey": "Bearer"
      }
    }
  ],
  "GlobalConfiguration": {
    "BaseUrl": "http://localhost:5000"
  }
}
```

### Anti-Patterns to Avoid

- **Infrastructure references in domain/application:** `Storage.Domain.csproj` and `Storage.Application.csproj` must have zero `<ProjectReference>` to any `Storage.Infrastructure.*`. Enforce by adding a Directory.Build.targets analyzer or a build-time test.
- **`File` class name conflict:** C# has `System.IO.File`. Name the entity class `FileEntity` in the infrastructure layer (EF mapping) but keep `File` in the domain. Add `using File = Storage.Domain.Entities.File;` in files that need both.
- **Skipping the test project in Phase 1:** Success criteria 3, 4, and 5 are only machine-verifiable with a test project. `Storage.Domain.Tests` must be created in Phase 1.
- **Using `dotnet new sln` default in .NET 10:** .NET 10 SDK defaults `dotnet new sln` to SLNX format. Pass `--format sln` if your CI tooling does not yet support SLNX, or embrace SLNX if it does.
- **`latest` Docker image tags:** Never use `:latest` in docker-compose.yml. Pin specific versions (see Docker Compose section below).

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Solution file management | Manual `.sln` XML editing | `dotnet sln add` CLI | Error-prone; CLI handles GUIDs and project type detection |
| Regex-based state machine | `if/else` chain for transitions | `switch` expression with tuple pattern | Single-expression exhaustiveness, compiler-checked |
| Docker health checks | Shell polling scripts | Native `healthcheck:` in compose YAML | Docker handles restart logic and `depends_on` condition |
| Angular workspace structure | Manual `tsconfig` linking | `ng generate library` | Handles tsconfig path mappings, public API barrel exports |

**Key insight:** The domain code in Phase 1 is deliberately infrastructure-free. The temptation to add EF Core annotations or MediatR to the domain is the primary thing to avoid.

---

## Common Pitfalls

### Pitfall 1: `System.IO.File` Naming Collision
**What goes wrong:** `Storage.Domain.Entities.File` conflicts with `System.IO.File` anywhere `System.IO` is in scope.
**Why it happens:** The architecture mandates the class be named `File` (matching the domain concept).
**How to avoid:** In `Storage.Api` and infrastructure projects, use fully qualified names or a using alias: `using DomainFile = Storage.Domain.Entities.File;`
**Warning signs:** Compiler error CS0104 ("ambiguous reference") on `File.Create(...)`.

### Pitfall 2: Solution File Format (SLNX vs SLN)
**What goes wrong:** .NET 10 SDK's `dotnet new sln` defaults to `.slnx` format. Some CI runners on older agents, older Visual Studio versions (pre-2026), or Rider versions may not open SLNX files correctly.
**Why it happens:** Breaking change in .NET 10 SDK — SLNX is now the default.
**How to avoid:** Use `dotnet new sln --format sln` to generate the traditional `.sln` format, OR confirm that all team tooling supports SLNX.
**Warning signs:** "Solution format not supported" error on open.

### Pitfall 3: Ocelot Requires a Separate Host Project
**What goes wrong:** Attempting to host Ocelot middleware inside `Storage.Api` instead of a separate project.
**Why it happens:** The architecture specifies Ocelot as the gateway — it should be a distinct service in compose.
**How to avoid:** Create a `Storage.Gateway` (or `OcelotGateway`) project with its own `Dockerfile` and `ocelot.json`. `Storage.Api` should not reference Ocelot.

### Pitfall 4: Docker Compose Service Startup Order
**What goes wrong:** Storage API starts before SQL Server, Redis, or Keycloak are ready, causing connection failures.
**Why it happens:** `depends_on` with just a service name only waits for container start, not service readiness.
**How to avoid:** Use `depends_on:` with `condition: service_healthy` and define `healthcheck:` on each dependency service. See Docker Compose section for exact syntax.

### Pitfall 5: Domain Event Count Mismatch
**What goes wrong:** Adding `file.preview_ready` as a 7th event "to be complete."
**Why it happens:** Architecture §8.2 lists 7 events; DOMAIN-06 lists 6. Preview-ready is a Phase 1 out-of-scope.
**How to avoid:** Implement exactly the 6 events listed in DOMAIN-06. `FilePreviewReadyEvent` is added in a later phase.

### Pitfall 6: Storage.Application Abstractions Folder Confusion
**What goes wrong:** Adding port interfaces (`IFileStorageProvider`, etc.) to `Storage.Application/Abstractions/` in Phase 1.
**Why it happens:** The folder is referenced in the architecture doc and CONTEXT says "leave the folder stub in Phase 1."
**How to avoid:** Create the `Abstractions/` directory with a `.gitkeep` or a placeholder comment file. Do NOT add the interfaces — they belong to Phase 2.

### Pitfall 7: Keycloak Container First-Boot Latency
**What goes wrong:** Ocelot cannot validate JWTs because Keycloak hasn't finished importing its realm on first boot. Healthcheck passes but realm endpoint returns 404.
**Why it happens:** Keycloak realm import runs after the container marks itself healthy.
**How to avoid:** In Phase 1, the `/health` endpoint on `Storage.Api` should not require JWT validation. Ocelot JWT validation can be disabled per-route during initial bring-up testing. Document this as a known first-boot behaviour.

---

## Code Examples

Verified patterns from official/architecture sources:

### File Status State Machine (success criterion #3)

```csharp
// All valid transitions per architecture §6.4 + CLAUDE.md:
// pending → scanning
// scanning → ready
// scanning → quarantined
// ready → deleted
// All other transitions: InvalidStatusTransitionException

[Fact]
public void Transition_PendingToScanning_Succeeds()
{
    var file = File.Create(/* ... */);  // Status = Pending
    file.Transition(FileStatus.Scanning);
    file.Status.Should().Be(FileStatus.Scanning);
}

[Fact]
public void Transition_PendingToReady_ThrowsDomainException()
{
    var file = File.Create(/* ... */);
    var act = () => file.Transition(FileStatus.Ready);
    act.Should().Throw<InvalidStatusTransitionException>();
}
```

### StorageKey Validation (success criterion #4)

```csharp
[Theory]
[InlineData("not-a-valid-key")]
[InlineData("tenantid/2024/01/01/fileid")]        // not GUIDs
[InlineData("")]
public void StorageKey_InvalidFormat_Throws(string value)
{
    var act = () => new StorageKey(value);
    act.Should().Throw<InvalidStorageKeyException>();
}

[Fact]
public void StorageKey_Create_ProducesCorrectFormat()
{
    var tenant = Guid.NewGuid();
    var file   = Guid.NewGuid();
    var date   = new DateOnly(2024, 1, 5);

    var key = StorageKey.Create(tenant, date, file);

    key.Value.Should().MatchRegex(
        @"^[0-9a-f-]{36}/2024/01/05/[0-9a-f-]{36}$",
        // NOTE: illustrative pattern (permissive). Production code enforces strict GUID regex.
        because: "storage key must match <tenantId>/<yyyy>/<mm>/<dd>/<uuid>");
}
```

### Checksum Normalisation (success criterion #4)

```csharp
[Fact]
public void Checksum_NormalisesToLowercase()
{
    var upper = new string('A', 64);  // 64 'A' chars — valid hex, uppercase
    var cs = new Checksum(upper);
    cs.Value.Should().Be(upper.ToLowerInvariant());
}

[Theory]
[InlineData("not-hex")]
[InlineData("abc123")]         // too short
[InlineData(null)]
public void Checksum_Invalid_Throws(string? value)
{
    var act = () => new Checksum(value!);
    act.Should().Throw<DomainException>();
}
```

### Docker Compose with Health Checks

```yaml
# docker-compose.yml (excerpt — pinned tags, health checks, depends_on conditions)
services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04
    environment:
      SA_PASSWORD: "YourStrong!Passw0rd"
      ACCEPT_EULA: "Y"
    ports: ["1433:1433"]
    healthcheck:
      test: /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "YourStrong!Passw0rd" -Q "SELECT 1" -b
      interval: 10s
      timeout: 5s
      retries: 10
      start_period: 30s

  redis:
    image: redis:7.4-alpine
    ports: ["6379:6379"]
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 5s
      timeout: 3s
      retries: 5

  rabbitmq:
    image: rabbitmq:4.0-management-alpine
    ports:
      - "5672:5672"
      - "15672:15672"
    healthcheck:
      test: ["CMD", "rabbitmq-diagnostics", "check_port_connectivity"]
      interval: 10s
      timeout: 5s
      retries: 10

  keycloak:
    image: quay.io/keycloak/keycloak:26.2
    command: start-dev --import-realm
    environment:
      KC_BOOTSTRAP_ADMIN_USERNAME: admin
      KC_BOOTSTRAP_ADMIN_PASSWORD: admin
    ports: ["8080:8080"]
    healthcheck:
      test: ["CMD-SHELL", "curl -fs http://localhost:8080/health/ready || exit 1"]
      interval: 15s
      timeout: 10s
      retries: 10
      start_period: 60s

  clamav:
    image: clamav/clamav:1.4
    ports: ["3310:3310"]
    healthcheck:
      test: ["CMD", "clamdscan", "--ping", "3"]
      interval: 30s
      timeout: 15s
      retries: 5
      start_period: 120s

  storage-api:
    build: ./backend/src/Storage.Api
    ports: ["8081:8080"]
    depends_on:
      sqlserver: { condition: service_healthy }
      redis:     { condition: service_healthy }
      rabbitmq:  { condition: service_healthy }
      keycloak:  { condition: service_healthy }
      clamav:    { condition: service_started }   # ClamAV healthcheck takes long

  ocelot-gateway:
    build: ./backend/src/Storage.Gateway
    ports: ["5000:8080"]
    depends_on:
      storage-api: { condition: service_started }
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `.sln` solution file (XML) | `.slnx` (default in .NET 10 SDK) | .NET 10 SDK (Nov 2025) | Pass `--format sln` if tooling doesn't support SLNX yet |
| `dotnet new webapi --controllers` | `dotnet new webapi` (minimal APIs default, no extra flag needed) | .NET 7+ | `--use-minimal-apis` flag removed in .NET 7+; minimal APIs is now the default template |
| xUnit v2 | xUnit v3 (`xunit.v3`) | 2024/2025 — v3 GA | v3 supports Microsoft Testing Platform; use `xunit.v3` packages |
| `KC_ADMIN_*` Keycloak env vars | `KC_BOOTSTRAP_ADMIN_*` | Keycloak 26+ | Old env var names are deprecated |

**Deprecated/outdated:**
- `Keycloak` env `KEYCLOAK_ADMIN` / `KEYCLOAK_ADMIN_PASSWORD`: replaced by `KC_BOOTSTRAP_ADMIN_USERNAME` / `KC_BOOTSTRAP_ADMIN_PASSWORD` in Keycloak 26+.

---

## Open Questions

1. **Ocelot .NET 10 target framework moniker**
   - What we know: Ocelot 24.1.0 targets net8.0/net9.0 but runs on .NET 10 runtime via backward compatibility. Ocelot 25.0.0-beta.2 explicitly targets net10.0 but is in beta.
   - What's unclear: Whether the project's CI or team tooling requires a net10.0 TFM on all projects.
   - Recommendation: Use Ocelot 24.1.0 (stable) for Phase 1. Document the plan to upgrade to 25.x GA when it ships. If net10.0 TFM is a hard requirement, use 25.0.0-beta.2 with a comment.

2. **Keycloak realm import for Phase 1**
   - What we know: Keycloak needs a realm with the `storage-service` audience configured so Ocelot can validate JWTs.
   - What's unclear: Whether a minimal `realm-export.json` is needed in Phase 1 or whether JWT validation can be bypassed for the `/health` smoke test.
   - Recommendation: Create a minimal `realm-export.json` committed to `backend/keycloak/` so `docker-compose up` fully succeeds. Skip JWT validation on `/health` endpoint (Phase 1 only).

3. **ClamAV first-scan database download latency**
   - What we know: `clamav/clamav:1.4` downloads virus definitions on first run (can take 5–15 minutes on a cold start).
   - What's unclear: Whether Phase 1 needs ClamAV actually functional or just running.
   - Recommendation: Use `clamav/clamav:1.4` (has pre-loaded signatures). Use `condition: service_started` (not `service_healthy`) for the storage-api dependency on clamav to avoid blocking startup.

---

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit v3 (`xunit.v3` 3.2.x) |
| Config file | None — Wave 0 creates `backend/tests/Storage.Domain.Tests/Storage.Domain.Tests.csproj` |
| Quick run command | `dotnet test backend/tests/Storage.Domain.Tests/Storage.Domain.Tests.csproj -v normal` |
| Full suite command | `dotnet test backend/tests/Storage.Domain.Tests/Storage.Domain.Tests.csproj -v normal` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| DOMAIN-01 | Valid transitions (pending→scanning, scanning→ready, scanning→quarantined, ready→deleted) succeed; invalid transitions throw `InvalidStatusTransitionException` | unit | `dotnet test ... --filter "FullyQualifiedName~FileStatusTransition"` | Wave 0 |
| DOMAIN-02 | `FileCategory.Validate()` returns failure for oversize, wrong MIME, wrong extension | unit | `dotnet test ... --filter "FullyQualifiedName~FileCategoryValidation"` | Wave 0 |
| DOMAIN-03 | `StorageKey` rejects malformed keys; `StorageKey.Create()` produces correct format | unit | `dotnet test ... --filter "FullyQualifiedName~StorageKey"` | Wave 0 |
| DOMAIN-04 | `Checksum` rejects non-hex-64 strings; normalises uppercase to lower | unit | `dotnet test ... --filter "FullyQualifiedName~Checksum"` | Wave 0 |
| DOMAIN-05 | `File` aggregate exposes `Versions`, `Permissions`, `Tags`, `AuditEntries` collections | unit | `dotnet test ... --filter "FullyQualifiedName~FileCollections"` | Wave 0 |
| DOMAIN-06 | All 6 event types are instantiable and carry required fields | unit | `dotnet test ... --filter "FullyQualifiedName~DomainEvents"` | Wave 0 |
| SETUP-01 | `dotnet build backend/StorageService.sln` passes; `Storage.Domain` and `Storage.Application` have zero `<ProjectReference>` to Infrastructure | build | `dotnet build backend/StorageService.sln -warnaserror` | Wave 0 |
| SETUP-03/05 | `docker-compose up` starts all services; Ocelot responds on port 5000 | smoke | `curl -f http://localhost:5000/health` | manual |

### Sampling Rate

- **Per task commit:** `dotnet test backend/tests/Storage.Domain.Tests/Storage.Domain.Tests.csproj -v normal`
- **Per wave merge:** same (only one test project in Phase 1)
- **Phase gate:** Full domain test suite green + `docker-compose up` smoke test before `/gsd:verify-work`

### Wave 0 Gaps

- [ ] `backend/tests/Storage.Domain.Tests/Storage.Domain.Tests.csproj` — covers DOMAIN-01 through DOMAIN-06
- [ ] `backend/tests/Storage.Domain.Tests/FileStatusTransitionTests.cs`
- [ ] `backend/tests/Storage.Domain.Tests/StorageKeyTests.cs`
- [ ] `backend/tests/Storage.Domain.Tests/ChecksumTests.cs`
- [ ] `backend/tests/Storage.Domain.Tests/FileCategoryValidationTests.cs`
- [ ] `backend/tests/Storage.Domain.Tests/DomainEventTests.cs`
- [ ] Framework install: `dotnet new xunit -n Storage.Domain.Tests -f net10.0 -o backend/tests/Storage.Domain.Tests`

---

## Sources

### Primary (HIGH confidence)

- Architecture doc `storage-microservice-architecture.md` §6, §8, §10 — domain model contracts, port interfaces, adapter patterns, data model
- `CLAUDE.md` — solution structure, stack constraints, key design decisions
- `.planning/phases/01-solution-scaffold-domain-model/01-CONTEXT.md` — locked decisions
- [Microsoft Learn: Seedwork domain base classes](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/seedwork-domain-model-base-classes-interfaces) — EntityBase/domain event patterns
- [Microsoft Learn: Domain events design](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation) — domain event pattern

### Secondary (MEDIUM confidence)

- [.NET 10 download page](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) — .NET 10.0 GA Nov 2025; current patch 10.0.x
- [EF Core 10.0 NuGet](https://www.nuget.org/packages/microsoft.entityframeworkcore) — EF Core 10.0.x available
- [Ocelot releases — ThreeMammals/Ocelot](https://github.com/ThreeMammals/Ocelot/releases) — 24.1.0 stable (net8/9); 25.0.0-beta.2 adds net10.0 TFM
- [Angular endoflife.date](https://endoflife.date/angular) — Angular 21.2.x/22.x current as of May 2026
- [RabbitMQ Docker Hub](https://hub.docker.com/_/rabbitmq/) — `rabbitmq:4.0-management-alpine` current
- [Keycloak quay.io](https://quay.io/repository/keycloak/keycloak) — `quay.io/keycloak/keycloak:26.2`
- [ClamAV Docker Hub](https://hub.docker.com/r/clamav/clamav/tags) — `clamav/clamav:1.4`
- [FluentAssertions NuGet](https://www.nuget.org/packages/FluentAssertions) — 8.9.x
- [NSubstitute NuGet](https://www.nuget.org/packages/nsubstitute/) — 5.3.x
- [xunit.v3 NuGet](https://www.nuget.org/packages/xunit.v3) — 3.2.x

### Tertiary (LOW confidence)

- [Breaking change: dotnet new sln defaults to SLNX](https://learn.microsoft.com/en-us/dotnet/core/compatibility/sdk/10.0/dotnet-new-sln-slnx-default) — verified from Microsoft Learn, but exact CI tool compat with SLNX is environment-dependent

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all core library versions verified via NuGet/official sources
- Architecture patterns: HIGH — derived directly from architecture doc §10 contracts
- Docker Compose: MEDIUM — image tags verified from Docker Hub; exact Keycloak realm config is environment-specific
- Pitfalls: HIGH — verified from official breaking-change docs and architecture doc cross-references

**Research date:** 2026-05-15
**Valid until:** 2026-06-15 (stable libraries; Ocelot 25.x GA may ship before then)
