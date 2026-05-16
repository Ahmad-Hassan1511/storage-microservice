---
gsd_state_version: 1.0
milestone: v1.0
milestone_name: milestone
current_phase: 10
current_plan: complete
status: Complete
last_updated: "2026-05-16T17:00:00.000Z"
progress:
  total_phases: 10
  completed_phases: 10
  total_plans: 10
  completed_plans: 10
  percent: 100
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

**Current Phase:** 10
**Current Plan:** complete
**Status:** Complete

```
Progress: [██████████] 100%

Phase  1 [x] Solution Scaffold & Domain Model
Phase  2 [x] Application Layer & Port Interfaces
Phase  3 [x] Persistence Adapter
Phase  4 [x] Storage Adapters
Phase  5 [x] Cache & Messaging Adapters
Phase  6 [x] REST API Layer
Phase  7 [x] Client SDK & Sample Microservices
Phase  8 [x] Angular Frontend
Phase  9 [x] Unit, Integration & API Tests
Phase 10 [x] E2E & Security Tests
```

---

## Performance Metrics

| Metric | Value |
|--------|-------|
| Phases defined | 10 |
| Requirements mapped | 95/95 |
| Phases complete | 10/10 |
| Build status | ✅ 0 warnings, 0 errors |
| Angular builds | ✅ shared-lib + demo-app |

---

## Accumulated Context

### Key Decisions

| Decision | Rationale | Status |
|----------|-----------|--------|
| Single upload endpoint `POST /v1/files` driven by `category` | Keeps validation centralised; avoids per-type endpoint proliferation | Confirmed |
| Pre-signed URLs as default byte transfer | Service stays stateless and cheap; never proxies bandwidth | Confirmed |
| `StorageCapabilities` flags as the single branch point | FileSystem adapter transparently falls back to proxy upload | Confirmed |
| MassTransit over raw broker clients | Single API for RabbitMQ + Azure Service Bus; broker is a config change | Confirmed |
| Testcontainers for integration tests | Real backends in CI without external dependencies or manual setup | Confirmed |
| Local backend/NuGet.Config with wildcard packageSourceMapping | Global NuGet config restricts package sources; local override clears mapping | Confirmed |
| Storage.Gateway added as 13th src project | Ocelot in its own ASP.NET Core host project | Confirmed |
| Angular CLI 17.3.6 used | Angular 22 not yet stable; Node 20.12.2 incompatible with Angular CLI 21+ | Confirmed |
| FileCategory.Validate() returns (bool, string?) tuple | Does NOT throw; intentionally different from File.Transition() | Confirmed |
| Storage.Domain.csproj has zero PackageReferences | Architecture constraint: domain must have no infrastructure dependencies | Confirmed |
| Exactly 6 domain event types; FilePreviewReadyEvent excluded | Per RESEARCH Pitfall 5 — preview events are Phase 1 out-of-scope | Confirmed |
| Ocelot 24.1.0 stable | net8.0 TFM backward compat works on .NET 10 runtime | Confirmed |
| Keycloak healthcheck via wget on management port 9000 | UBI-minimal image has no curl | Confirmed |
| ClamAV condition:service_started | Definition download takes 5-15 min on first boot | Confirmed |
| Docker build context is repo root | Ensures all backend COPY paths work | Confirmed |
| IntegrationEvent separate record hierarchy from DomainEvent | Message-bus transport must not leak domain concepts | Confirmed |
| IUnitOfWork exposes IFileCategoryRepository Categories sub-port | Keeps categories inside unit-of-work transaction scope | Confirmed |
| Storage.Application.csproj has zero PackageReferences | Application core depends only on BCL | Confirmed |
| dotnet-ef 9.0.4 used for migration generation | Global NuGet packageSourceMapping excludes 10.0.8 tool | Confirmed |
| Storage.Sdk is self-contained (no Application reference) | SDK should be deployable as standalone NuGet package | Confirmed |
| NullEventBus in Api.Tests | No broker required during WebApplicationFactory tests | Confirmed |
| TestAuthHandler returns preset test identity | All test requests get a valid tenant+principal | Confirmed |

### Architecture Constraints Confirmed

- Tech stack: .NET 10, C# latest, ASP.NET Core Minimal APIs, EF Core 10, Angular 17.3 — no deviations
- ORM: EF Core 10 only
- Messaging: MassTransit abstraction over RabbitMQ/Azure Service Bus
- Testing: xUnit + FluentAssertions + NSubstitute + Testcontainers; Playwright for E2E
- Demo object store: Wasabi free tier; Azure Blob via Azure.Storage.Blobs

### Out of Scope (Confirmed)

- AI content validation (v2)
- Terraform IaC / Azure provisioning (v2)
- Azure DevOps CI/CD pipelines (v2)
- k6 performance/load tests (v2)
- WebSocket real-time status updates (polling sufficient for v1)
- Multi-region Azure deployment (v1 is single-region demo)

### Todos

- None (all phases complete)

### Blockers

- (none)

### Notes

- Architecture source of truth: `storage-microservice-architecture.md` and `CLAUDE.md`
- Integration tests (Testcontainers) require Docker to run
- E2E tests require running API + frontend; set API_BASE_URL and APP_BASE_URL env vars

---

## Session Continuity

**To resume work:** All 10 phases are complete. The solution builds with 0 warnings under `-warnaserror`.

**Last session:** 2026-05-16T17:00:00.000Z

**Stopped at:** Phase 10 complete — E2E & Security Tests

**Next action:** No further implementation needed. Run tests when Docker is available.

---

*State initialized: 2026-05-15*
*Completed: 2026-05-16*
