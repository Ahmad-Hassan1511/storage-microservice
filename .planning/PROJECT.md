# Storage Microservice

## What This Is

A platform-wide file storage hub implemented as a .NET 10 microservice with an Angular frontend. It is the single service responsible for storing and retrieving binary content of any type; no other microservice owns file bytes directly. The project also includes two sample consumer microservices (Documents, Profile) that demonstrate the correct integration pattern.

## Core Value

Consumer microservices never proxy file bytes — they call this service to get a short-lived pre-signed URL, then the client uploads or downloads directly to the object store.

## Requirements

### Validated

(None yet — ship to validate)

### Active

- [ ] Full .NET 10 solution in `backend/` with Ports and Adapters architecture
- [ ] Angular workspace in `frontend/` with shared upload library and feature modules
- [ ] Storage Microservice: domain model, application use cases, all port interfaces
- [ ] Persistence adapter: EF Core 10 + SQL Server with all migrations
- [ ] Storage adapters: FileSystem (dev), Wasabi (demo), Azure Blob (prod)
- [ ] Cache adapters: InMemory (test), Redis (demo/prod)
- [ ] Messaging adapters: InMemory (test), RabbitMQ (demo), Azure Service Bus (prod)
- [ ] REST API: all `/v1/files` and `/v1/categories` endpoints per §6.2
- [ ] Configuration-driven DI wiring (provider selected by `appsettings`)
- [ ] Docker Compose local dev stack (SQL Server, Redis, RabbitMQ, Keycloak, ClamAV, Wasabi)
- [ ] Ocelot API gateway for demo environment
- [ ] Documents sample microservice with event-driven status sync
- [ ] Profile sample microservice with avatar thumbnail worker (ImageSharp)
- [ ] Client SDK (`Storage.Sdk`) with retry, idempotency-key, multipart chunking
- [ ] Reusable Angular `FileUploaderComponent` (small + large file, resume, crop)
- [ ] Angular Documents and Profile feature modules
- [ ] Unit tests: domain entities + application use cases (80% coverage gate)
- [ ] Integration tests: all adapters via Testcontainers
- [ ] API tests: full HTTP surface via WebApplicationFactory
- [ ] Contract tests: cross-adapter port semantics
- [ ] E2E tests: Playwright browser flows + k6 API scenarios
- [ ] Security tests: pre-signed URL TTL enforcement, multi-tenant isolation, OWASP baseline

### Out of Scope

- AI content validation (§6.6) — designed but deferred to future milestone; schema columns added but workers not implemented
- Terraform IaC / Azure provisioning — infrastructure is code but not provisioned in this milestone
- Azure DevOps CI/CD pipelines — pipeline YAML deferred; local test run is sufficient for milestone
- Performance/load tests (k6 sustained load scenarios) — separate tooling phase

## Context

Architecture source of truth: `storage-microservice-architecture.md` and `CLAUDE.md`.
Sections 6 (Low-Level Design) and 10 (Ports and Adapters) are the canonical contracts.

**Project layout:**
```
backend/     .NET 10 solution (src/ + tests/)
frontend/    Angular workspace (projects/shared-lib + projects/demo-app)
docker-compose.yml
```

**Key invariants to respect in all phases:**
- `Storage.Application` and `Storage.Domain` never reference Infrastructure projects
- Application branches exactly once on `Capabilities.SupportsPresignedUploadUrls`
- Events carry no file bytes — only metadata + IDs
- All file bytes flow through object store, never through the API service
- Storage key format: `<tenantId>/<yyyy>/<mm>/<dd>/<uuid>`
- File status machine: `pending → scanning → ready | quarantined`

## Constraints

- **Tech stack**: .NET 10, C# latest, ASP.NET Core Minimal APIs, EF Core 10, Angular latest stable — no deviations
- **ORM**: EF Core 10 only; raw ADO.NET only for performance-sensitive reporting queries
- **Messaging**: MassTransit abstraction over RabbitMQ/Azure Service Bus; do not use AMQP client directly
- **Testing**: xUnit + FluentAssertions + NSubstitute + Testcontainers; Playwright for E2E
- **Demo object store**: Wasabi free tier (AWSSDK.S3 with custom ServiceURL); Azure Blob via `Azure.Storage.Blobs`
- **Image processing**: ImageSharp for thumbnail generation in the Profile worker

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Single upload endpoint `POST /v1/files` driven by `category` | Keeps validation centralised; avoids per-type endpoint proliferation | — Pending |
| Pre-signed URLs as default byte transfer | Service stays stateless and cheap; never proxies bandwidth | — Pending |
| `StorageCapabilities` flags as the single branch point | File System adapter transparently falls back to proxy upload | — Pending |
| MassTransit over raw broker clients | Single API for RabbitMQ + Azure Service Bus; broker is a config change | — Pending |
| Testcontainers for integration tests | Real backends in CI without external dependencies or manual setup | — Pending |

---
*Last updated: 2026-05-15 after initialization*
