# Phase 1: Solution Scaffold & Domain Model - Context

**Gathered:** 2026-05-15
**Status:** Ready for planning

<domain>
## Phase Boundary

Create the `backend/StorageService.sln` solution with all projects from §10.2, the Angular workspace skeleton in `frontend/`, the root `docker-compose.yml` for the full local dev stack, and the domain core (File entity state machine, FileCategory policy, value objects, domain events). Nothing is wired to infrastructure in this phase.

</domain>

<decisions>
## Implementation Decisions

### Solution & Namespace Naming
- Namespace prefix: `Storage.*` — matches the architecture doc exactly
- Solution file: `backend/StorageService.sln`
- Projects: `Storage.Domain`, `Storage.Application`, `Storage.Infrastructure.Persistence.SqlServer`, `Storage.Infrastructure.Storage.Wasabi`, `Storage.Infrastructure.Storage.AzureBlob`, `Storage.Infrastructure.Storage.FileSystem`, `Storage.Infrastructure.Cache.Redis`, `Storage.Infrastructure.Cache.InMemory`, `Storage.Infrastructure.Messaging.RabbitMQ`, `Storage.Infrastructure.Messaging.AzureServiceBus`, `Storage.Sdk`, `Storage.Api`

### Domain Entity Style
- `File` and all domain entities are mutable `class` types — not records
- State transitions are encapsulated methods: `file.Transition(FileStatus.Scanning)` throws `DomainException` for invalid transitions
- Domain rule violations signal via `DomainException` (custom exception, not Result<T>)
- API layer catches `DomainException` and maps to 422 Unprocessable Entity

### Domain Events
- Domain events are plain C# `record` types in `Storage.Domain` — no dispatcher wired in Phase 1
- Entities inherit from `abstract EntityBase` which holds `List<DomainEvent> _events`, exposes `RaiseDomainEvent()` and `ClearDomainEvents()`
- MassTransit wires up dispatching in Phase 5; nothing dispatches events until then

### Docker Compose DX
- Single `docker-compose.yml` at repo root starts all backend services: SQL Server, Redis, RabbitMQ, Keycloak, ClamAV, Storage API, Ocelot gateway
- Angular runs locally via `ng serve` — not in Docker Compose
- Developer workflow: `docker-compose up` for backend, `ng serve` for frontend

### Claude's Discretion
- Exact `EntityBase` generic typing (e.g., `EntityBase<TId>` vs `EntityBase`)
- `DomainException` hierarchy (single class or subclasses per rule)
- Angular workspace library project names within `frontend/`

</decisions>

<specifics>
## Specific Ideas

- Architecture doc §10.2 is the canonical project list — follow it exactly
- `Storage.Application` and `Storage.Domain` must have zero `<ProjectReference>` to any `Storage.Infrastructure.*` — enforced by build
- Storage key format is non-negotiable: `<tenantId>/<yyyy>/<mm>/<dd>/<uuid>`
- File status state machine is the first thing that must work and be tested

</specifics>

<code_context>
## Existing Code Insights

### Reusable Assets
- None — greenfield project

### Established Patterns
- Architecture doc §10 defines all interface contracts that will constrain Phase 2+
- `StorageCapabilities` record pattern from §10.3 is the branching model for all storage adapters

### Integration Points
- `Storage.Api` will reference all Infrastructure projects and wire adapters at startup (Phase 6)
- `Storage.Application/Abstractions/` is where port interfaces land (Phase 2) — leave the folder stub in Phase 1

</code_context>

<deferred>
## Deferred Ideas

- None — discussion stayed within phase scope

</deferred>

---

*Phase: 01-solution-scaffold-domain-model*
*Context gathered: 2026-05-15*
