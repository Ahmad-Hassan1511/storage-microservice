---
gsd_state_version: 1.0
milestone: v1.0
milestone_name: milestone
current_phase: 3
current_plan: pending
status: In-progress
last_updated: "2026-05-16T06:00:00.000Z"
progress:
  total_phases: 10
  completed_phases: 2
  total_plans: 7
  completed_plans: 7
  percent: 20
---

# Project State: Storage Microservice

*Last updated: 2026-05-16*

---

## Project Reference

**Core Value:** Consumer microservices never proxy file bytes — they call this service for a pre-signed URL, then clients upload/download directly to the object store.

**Architecture:** .NET 10 Ports and Adapters microservice + Angular frontend. `Storage.Application` and `Storage.Domain` must never reference infrastructure projects.

**Key Invariants:**
- Events carry no file bytes — only metadata + IDs
- All file bytes flow through object store, never through the API service
- Storage key format: `<tenantId>/<yyyy>/<mm>/<dd>/<uuid>`
- File status machine: `pending → scanning → ready | quarantined | deleted`
- Application branches exactly once on `Capabilities.SupportsPresignedUploadUrls`

---

## Current Position

**Current Phase:** 3
**Current Plan:** pending
**Status:** In-progress

```
Progress: [██░░░░░░░░] 20%

Phase  1 [x] Solution Scaffold & Domain Model
Phase  2 [x] Application Layer & Port Interfaces
Phase  3 [ ] Persistence Adapter
Phase  4 [ ] Storage Adapters
Phase  5 [ ] Cache & Messaging Adapters
Phase  6 [ ] REST API Layer
Phase  7 [ ] Client SDK & Sample Microservices
Phase  8 [ ] Angular Frontend
Phase  9 [ ] Unit, Integration & API Tests
Phase 10 [ ] E2E & Security Tests
```

---

## Performance Metrics

| Metric | Value |
|--------|-------|
| Phases defined | 10 |
| Requirements mapped | 95/95 |
| Phases complete | 1/10 |
| Plans written | 3 (Phase 1) |
| Plans complete | 3 |
| Phase 01-solution-scaffold-domain-model P02 | 4 | 2 tasks | 26 files |
| Phase 01-solution-scaffold-domain-model P03 | 20 | 3 tasks | 9 files |
| Phase 02-application-layer-port-interfaces P01 | 22 | 3 tasks | 31 files |
| Phase 02-application-layer-port-interfaces P03 | 25 | 1 tasks | 5 files |
| Phase 02-application-layer-port-interfaces P04 | 18 | 2 tasks | 6 files |

### Execution History

| Phase-Plan | Duration | Tasks | Files |
|------------|----------|-------|-------|
| Phase 01-solution-scaffold-domain-model P01 | 50 min | 3 | 30 |
| Phase 02-application-layer-port-interfaces P01 | 22 min | 3 | 31 |

---

## Accumulated Context

### Key Decisions

| Decision | Rationale | Status |
|----------|-----------|--------|
| Single upload endpoint `POST /v1/files` driven by `category` | Keeps validation centralised; avoids per-type endpoint proliferation | Pending validation |
| Pre-signed URLs as default byte transfer | Service stays stateless and cheap; never proxies bandwidth | Pending validation |
| `StorageCapabilities` flags as the single branch point | FileSystem adapter transparently falls back to proxy upload | Pending validation |
| MassTransit over raw broker clients | Single API for RabbitMQ + Azure Service Bus; broker is a config change | Pending validation |
| Testcontainers for integration tests | Real backends in CI without external dependencies or manual setup | Pending validation |
| Local backend/NuGet.Config with wildcard packageSourceMapping | Global NuGet config restricts package sources; local override clears mapping to allow xunit.v3, FluentAssertions, Microsoft.AspNetCore.OpenApi | Confirmed |
| Storage.Gateway added as 13th src project | RESEARCH Pattern 6/Pitfall 3 requires Ocelot in its own ASP.NET Core host project | Confirmed |
| Angular CLI 17.3.6 used instead of Angular 22 | Angular 22 not yet stable (RC only); Node 20.12.2 incompatible with Angular CLI 21+; Angular 17 produces identical workspace structure | Confirmed |
| FileCategory.Validate() returns (bool, string?) tuple — does NOT throw | Intentionally different from File.Transition() which throws InvalidStatusTransitionException; validation returns result, state machine enforces invariants | Confirmed |
| Storage.Domain.csproj has zero PackageReferences (pure BCL) | Architecture constraint: domain must have no infrastructure dependencies; validated in Plan 02 | Confirmed |
| Exactly 6 domain event types; FilePreviewReadyEvent excluded | Per RESEARCH Pitfall 5 — preview events are Phase 1 out-of-scope | Confirmed |
| Ocelot 24.1.0 stable (not beta 25.x) for Storage.Gateway | net8.0 TFM backward compat works on .NET 10 runtime with zero NU1701 warnings; beta not appropriate | Confirmed |
| Keycloak healthcheck via wget on management port 9000 | UBI-minimal image has no curl; management port 9000 is the correct health probe target; application port 8080 does not expose /health/ready | Confirmed |
| ClamAV condition:service_started (not service_healthy) | Definition download takes 5-15 min on first boot; service_healthy would block storage-api startup unacceptably | Confirmed |
| Docker build context is repo root for both Dockerfiles | Ensures all backend COPY paths work; both Dockerfiles reference backend/src/* paths consistently | Confirmed |
| IntegrationEvent separate record hierarchy from DomainEvent | Message-bus transport must not leak domain concepts; separate namespace (Storage.Application.Events vs Storage.Domain.Events) | Confirmed |
| IUnitOfWork exposes IFileCategoryRepository Categories sub-port | Keeps categories inside the unit-of-work transaction scope; resolves RESEARCH open question | Confirmed |
| Storage.Application.csproj has zero PackageReferences | Application core depends only on BCL; RangeHeaderValue is System.Net.Http (BCL); no NuGet required | Confirmed |
| FileListQuery pulled from Task 2b into Task 2a | IFileRepository.ListAsync forward-references FileListQuery; build would fail if kept in 2b | Confirmed |

### Architecture Constraints Confirmed

- Tech stack: .NET 10, C# latest, ASP.NET Core Minimal APIs, EF Core 10, Angular latest stable — no deviations
- ORM: EF Core 10 only; raw ADO.NET only for performance-sensitive reporting queries
- Messaging: MassTransit abstraction over RabbitMQ/Azure Service Bus; do not use AMQP client directly
- Testing: xUnit + FluentAssertions + NSubstitute + Testcontainers; Playwright for E2E
- Demo object store: Wasabi free tier (AWSSDK.S3 with custom ServiceURL); Azure Blob via Azure.Storage.Blobs
- Image processing: ImageSharp for thumbnail generation in Profile worker

### Out of Scope (Confirmed)

- AI content validation — schema columns added but workers not implemented (v2)
- Terraform IaC / Azure provisioning (v2)
- Azure DevOps CI/CD pipelines (v2)
- k6 performance/load tests (v2)
- WebSocket real-time status updates (polling sufficient for v1)
- Multi-region Azure deployment (v1 is single-region demo)

### Todos

- Plan and execute Phase 3: Persistence Adapter (EF Core, SQL Server, repositories, migrations)

### Blockers

- (none)

### Notes

- Architecture source of truth: `storage-microservice-architecture.md` and `CLAUDE.md`
- Sections 6 (Low-Level Design) and 10 (Ports and Adapters) are canonical contracts
- Phase 4 (Storage Adapters) and Phase 3 (Persistence Adapter) both depend on Phase 2 (Application Layer) — they can be implemented in parallel once Phase 2 is complete
- Phase 5 (Cache & Messaging Adapters) also depends only on Phase 2 — potentially parallel with 3 and 4

---

## Session Continuity

**To resume work:** Read this file and `ROADMAP.md`. The current phase goal and success criteria in ROADMAP.md define what must be true before moving to the next phase.

**Last session:** 2026-05-15T23:34:27.137Z

**Next action:** Plan and execute Phase 3 — Persistence Adapter.

---

*State initialized: 2026-05-15*
