# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository Purpose

This directory contains the architecture design document for a **Storage Microservice** (`storage-microservice-architecture.md`). It is a design artifact, not a code repository. There are no build commands or test runners.

**Read `storage-microservice-architecture.md` first.** The document's own header identifies Sections 6 (Low-Level Design) and 10 (Ports and Adapters) as source-of-truth contracts for all implementation decisions.

---

## Architecture at a Glance

**Stack:** .NET 10 · Angular · SQL Server (EF Core 10) · S3-compatible object storage · Redis · RabbitMQ/Azure Service Bus (MassTransit) · Ocelot/Azure API Management

**Pattern:** Ports and Adapters (Hexagonal). The application core (`Storage.Application`, `Storage.Domain`) depends only on C# interfaces; concrete adapters are selected at startup by configuration. No application code changes between environments.

**Environment mapping:**

| Layer | Production (Azure) | Demo / Local |
|---|---|---|
| Object storage | Azure Blob Storage | Wasabi free tier; file system for tests |
| Database | Azure SQL Database | SQL Server in Docker |
| Cache | Azure Cache for Redis | Redis in Docker; in-memory for unit tests |
| Event bus | Azure Service Bus | RabbitMQ in Docker; in-memory for unit tests |
| Gateway | Azure API Management | Ocelot |
| Identity | Microsoft Entra ID | Keycloak |

---

## Solution Structure

```
src/
├── Storage.Domain/                                 # entities, value objects, domain events
├── Storage.Application/                            # use cases, DTOs, port interfaces
│   └── Abstractions/
│       ├── IFileStorageProvider.cs
│       ├── ICacheProvider.cs
│       ├── IEventBus.cs
│       └── IUnitOfWork.cs
├── Storage.Infrastructure.Persistence.SqlServer/   # EF Core, migrations, repositories
├── Storage.Infrastructure.Storage.Wasabi/
├── Storage.Infrastructure.Storage.AzureBlob/
├── Storage.Infrastructure.Storage.FileSystem/
├── Storage.Infrastructure.Cache.Redis/
├── Storage.Infrastructure.Cache.InMemory/
├── Storage.Infrastructure.Messaging.RabbitMQ/
├── Storage.Infrastructure.Messaging.AzureServiceBus/
├── Storage.Sdk/                                    # client library for consuming services
└── Storage.Api/                                    # minimal API host, DI wiring, OpenAPI
```

`Storage.Application` and `Storage.Domain` must not reference anything in the Infrastructure layer.

---

## Key Design Decisions

### File Categories drive all validation
Validation rules (max size, allowed MIME types, extensions, preview support, antivirus requirement) are properties of a **`FileCategory`** row, not of upload endpoints. The API has a single `POST /v1/files` endpoint; the `category` field selects the policy. Never add per-type endpoints.

### Storage key format
`<tenantId>/<yyyy>/<mm>/<dd>/<uuid>` — the date prefix prevents object-store hot partitions.

### File status lifecycle
`pending` → `scanning` → `ready` (or `quarantined`). Files are never served until `Status = ready`.

### Pre-signed URLs are the default byte transfer path
The Storage Microservice never proxies file bytes in the normal flow. `GET /v1/files/{id}` returns a short-lived pre-signed URL; the client talks directly to the object store. Proxied download (`GET /v1/files/{id}/content`) is the audited exception, not the default.

### `StorageCapabilities` is the branching point
The application layer branches exactly once — on `Capabilities.SupportsPresignedUploadUrls`. When false (file-system adapter), it falls through to the proxy upload path. Every other path is shared.

### `ICacheProvider.IsDistributed` guard
Operations requiring distributed guarantees (idempotency keys, rate-limit counters, distributed locks) must check `IsDistributed` and fall back to SQL Server when false (in-memory adapter in tests/single-node).

### Events carry no file bytes
Every domain event (`file.created`, `file.uploaded`, `file.scanned`, `file.ready`, `file.deleted`, etc.) carries only `fileId`, `tenantId`, `ownerService`, timestamp, and routing metadata — never payload bytes.

### AI validation is future/opt-in
AI content validation (`IAiValidationProvider`) is designed but not implemented in the initial release. It is gated per-category via `RequiresAiValidation` and `AiValidationStrategy` columns.

---

## REST API Surface (§6.2)

All endpoints versioned at `/v1`. Key ones:

| Endpoint | Purpose |
|---|---|
| `POST /v1/files` | Initiate upload — validates category, returns `{ fileId, uploadUrl, uploadHeaders, expiresAt, proxyRequired, multipartRequired }` |
| `POST /v1/files/{id}/complete` | Confirm upload, trigger antivirus, publish `file.uploaded` |
| `POST /v1/files/{id}/parts` | Multipart: issue per-part pre-signed URLs |
| `GET /v1/files/{id}` | Metadata + short-lived `downloadUrl`, `previewUrl`, `thumbnailUrl` |
| `GET /v1/files/{id}/content` | Audited proxy download (exception path) |
| `GET /v1/categories` | List all `FileCategories` |

Common headers: `Authorization: Bearer <jwt>`, `Idempotency-Key`, `X-Tenant-Id`, `traceparent`.

---

## Data Model Highlights (§6.3)

- `Files` — aggregate root; `Status` constrained to `('pending','scanning','ready','quarantined','deleted')`; `PreviewFileId` and `ThumbnailFileId` are self-references to generated preview files.
- `FileCategories` — policy table; one row per content type.
- `FileVersions` — `(FileId, VersionNumber)` PK; each version has its own `StorageKey`.
- `FilePermissions` — per-file ACL; `PrincipalType` is `'service'` or `'user'`.
- `AuditLog` — append-only; every mutation recorded.

---

## Inter-Service Communication (§8)

1. **REST (sync):** consuming microservices call `/v1/files` for all CRUD. They never proxy bytes through this service.
2. **Events (async):** MassTransit over RabbitMQ (demo) or Azure Service Bus (prod). Subscribe to `file.ready` instead of polling.
3. **Pre-signed URLs:** bandwidth-intensive byte transfer handed off to the object store.

Consumers must: send `Idempotency-Key` on POSTs, honour `Retry-After` on 429s, use exponential backoff with jitter, treat the service as eventually consistent for newly uploaded files.
