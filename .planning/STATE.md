---
gsd_state_version: 1.0
milestone: v1.0
milestone_name: milestone
current_phase: Phase 1 — Solution Scaffold & Domain Model
current_plan: Plan 02 (01-02-PLAN.md)
status: In progress
last_updated: "2026-05-16T00:50:00.000Z"
progress:
  total_phases: 10
  completed_phases: 0
  total_plans: 3
  completed_plans: 1
  percent: 33
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

**Current Phase:** Phase 1 — Solution Scaffold & Domain Model
**Current Plan:** Plan 02 — Domain core: entities, value objects, events, state machine
**Status:** In progress — Plan 01 complete, Plan 02 is next

```
Progress: [███░░░░░░░] 33%

Phase  1 [ ] Solution Scaffold & Domain Model
Phase  2 [ ] Application Layer & Port Interfaces
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
| Phases complete | 0/10 |
| Plans written | 3 (Phase 1) |
| Plans complete | 1 |

### Execution History

| Phase-Plan | Duration | Tasks | Files |
|------------|----------|-------|-------|
| Phase 01-solution-scaffold-domain-model P01 | 50 min | 3 | 30 |

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

- Plan 02: Domain core TDD (entities, value objects, events, state machine)
- Plan 03: Docker Compose stack + Ocelot gateway (has checkpoint)

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

**Last session:** Completed Plan 01 (solution scaffold) on 2026-05-16. Stopped at: Plan 02 ready to execute.

**Next action:** Execute 01-02-PLAN.md (Domain core: entities, value objects, events, state machine — TDD wave).

---

*State initialized: 2026-05-15*
