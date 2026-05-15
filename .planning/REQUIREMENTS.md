# Requirements: Storage Microservice

**Defined:** 2026-05-15
**Core Value:** Consumer microservices never proxy file bytes — they call this service for a pre-signed URL, then clients upload/download directly to the object store.

## v1 Requirements

### Setup & Scaffolding

- [x] **SETUP-01**: Backend .NET 10 solution exists in `backend/` with all projects from §10.2 (`Storage.Domain`, `Storage.Application`, `Storage.Infrastructure.*`, `Storage.Api`, `Storage.Sdk`)
- [x] **SETUP-02**: Angular workspace exists in `frontend/` with a shared library project and a demo app
- [x] **SETUP-03**: Root `docker-compose.yml` brings up SQL Server, Redis, RabbitMQ, Keycloak, ClamAV, and the Storage API service
- [x] **SETUP-04**: Ocelot gateway project routes `/storage/v1/*` → Storage API and `/api/documents/*` → Documents API with JWT validation against Keycloak
- [x] **SETUP-05**: Developer can run the full local stack with `docker-compose up` and hit the Ocelot gateway on port 5000

### Domain Model

- [x] **DOMAIN-01**: `File` entity implements the status state machine (`pending → scanning → ready | quarantined | deleted`); invalid transitions are rejected
- [x] **DOMAIN-02**: `FileCategory` entity holds all policy fields from §6.1 (`MaxSizeBytes`, `AllowedMimeTypes`, `AllowedExtensions`, `IsLargeFile`, `SupportsPreview`, `AntivirusRequired`, etc.) and exposes a `Validate(file)` method
- [x] **DOMAIN-03**: `StorageKey` value object enforces `<tenantId>/<yyyy>/<mm>/<dd>/<uuid>` format
- [x] **DOMAIN-04**: `Checksum` value object stores SHA-256 hex, normalises casing, and rejects malformed values
- [x] **DOMAIN-05**: `FileVersion`, `FilePermission`, `FileTag`, `AuditEntry` entities exist as owned collections of `File`
- [x] **DOMAIN-06**: Domain events (`FileCreatedEvent`, `FileUploadedEvent`, `FileScannedEvent`, `FileReadyEvent`, `FileDeletedEvent`, `FilePermissionChangedEvent`) are defined in `Storage.Domain`

### Application Layer

- [ ] **APP-01**: `IFileStorageProvider`, `ICacheProvider`, `IEventBus`, `IUnitOfWork` port interfaces are defined in `Storage.Application/Abstractions/`
- [ ] **APP-02**: `UploadService.InitiateUploadAsync` validates against `FileCategory` policy (size, MIME, extension, allowed owner services) and returns `{ fileId, uploadUrl, uploadHeaders, expiresAt, proxyRequired, multipartRequired }`
- [ ] **APP-03**: `UploadService.InitiateUploadAsync` enforces idempotency via `Idempotency-Key`; same key same payload returns same `FileId`; same key different payload returns 422
- [ ] **APP-04**: `UploadService.CompleteUploadAsync` verifies SHA-256 checksum, transitions status to `scanning`, and publishes `file.uploaded`
- [ ] **APP-05**: `DownloadService.GetFileAsync` authorises caller (service-level + user-level ACL), reads metadata (cache-first), and returns a fresh pre-signed download URL (or CDN URL for public files)
- [ ] **APP-06**: `DownloadService.GetFileStreamAsync` proxies bytes for the audited download path (when `proxyRequired=true`)
- [ ] **APP-07**: `FileManagementService` handles metadata patch, soft delete, hard delete (admin scope), version creation, listing with cursor-based pagination, and share link generation

### Persistence Adapter

- [ ] **PERSIST-01**: `StorageDbContext` contains `DbSet` for all tables: `Files`, `FileCategories`, `FileVersions`, `FilePermissions`, `FileTags`, `AuditLog`
- [ ] **PERSIST-02**: EF Core entity configurations (Fluent API) match the SQL schema from §6.3 including all indexes and check constraints
- [ ] **PERSIST-03**: Initial migration creates the full schema; a seeder inserts the starter `FileCategory` rows from §6.1
- [ ] **PERSIST-04**: `EfUnitOfWork` implements `IUnitOfWork` with `ExecuteInTransactionAsync` and provides typed repository properties
- [ ] **PERSIST-05**: `FileRepository` implements soft-delete query filter (deleted rows excluded from default queries) and all query methods needed by use cases

### Storage Adapters

- [ ] **STORE-01**: `FileSystemStorageProvider` writes/reads files from a configured root directory; returns `ProxyRequired=true` in `StoragePutResult`; `Capabilities` flags `SupportsPresignedUploadUrls=false`
- [ ] **STORE-02**: `WasabiStorageProvider` generates pre-signed PUT and GET URLs via AWSSDK.S3 with custom `ServiceURL`; `Capabilities` reflects full S3 support
- [ ] **STORE-03**: `AzureBlobStorageProvider` generates SAS URIs via `Azure.Storage.Blobs`; authenticates with managed identity in production
- [ ] **STORE-04**: All three adapters implement `OpenReadStreamAsync` with HTTP Range support
- [ ] **STORE-05**: `AddStorageProvider(cfg)` extension selects the adapter by `Storage:Provider` config key; unknown value throws at startup
- [ ] **STORE-06**: Contract test suite (`Storage.Contract.Tests`) verifies `PutObject`/`GetObject` roundtrip and `Capabilities` accuracy across all three adapters

### Cache Adapters

- [ ] **CACHE-01**: `InMemoryCacheProvider` uses `IMemoryCache`; `IsDistributed=false`
- [ ] **CACHE-02**: `RedisCacheProvider` uses `StackExchange.Redis`; `IsDistributed=true`; supports distributed lock via `SETNX`
- [ ] **CACHE-03**: Pre-signed URLs are cached in Redis under `presigned:get:{fileId}:{principal}` with TTL slightly shorter than the URL itself
- [ ] **CACHE-04**: Hot metadata is cached per `file:{fileId}` and invalidated on any mutation
- [ ] **CACHE-05**: Cache contract tests verify TTL semantics, distributed lock behaviour, and `IsDistributed` flag accuracy

### Messaging Adapters

- [ ] **MSG-01**: `IEventBus` thin facade over MassTransit's `IBus`
- [ ] **MSG-02**: `RabbitMqEventBus` and `AzureServiceBusEventBus` both implement `IEventBus` through MassTransit; switching is a config change (`EventBus:Provider`)
- [ ] **MSG-03**: `InMemoryEventBus` (backed by MassTransit in-memory transport) used in unit and API tests
- [ ] **MSG-04**: All domain events (`file.created`, `file.uploaded`, `file.scanned`, `file.ready`, `file.deleted`, `file.permission_changed`) are published and routable
- [ ] **MSG-05**: Dead-letter queue configured for handlers that exhaust retries; retry uses exponential backoff with jitter

### REST API

- [ ] **API-01**: `POST /v1/files` validates category policy and returns 201 with `{ fileId, uploadUrl, uploadHeaders, expiresAt, proxyRequired, multipartRequired }` or appropriate 4xx (400/403/413/415)
- [ ] **API-02**: `POST /v1/files/{id}/complete` confirms upload, verifies checksum, publishes `file.uploaded`; returns 200 or 409 on checksum mismatch
- [ ] **API-03**: `POST /v1/files/{id}/parts` and `POST /v1/files/{id}/parts/complete` support multipart upload flow
- [ ] **API-04**: `GET /v1/files/{id}` returns file metadata + `downloadUrl` (and `previewUrl`/`thumbnailUrl` if present) when status is `ready`; returns 404 for pending/scanning/cross-tenant
- [ ] **API-05**: `GET /v1/files/{id}/content` proxies byte stream with Range support; writes audit log entry
- [ ] **API-06**: `PATCH /v1/files/{id}` updates metadata, tags, and permissions
- [ ] **API-07**: `DELETE /v1/files/{id}` soft-deletes; `?hard=true` requires `storage.admin` scope
- [ ] **API-08**: `GET /v1/files` returns paginated listing with cursor; supports `owner`, `category`, `tag`, `mime`, date-range filters
- [ ] **API-09**: `POST /v1/files/{id}/versions`, `GET /v1/files/{id}/versions` manage versioning
- [ ] **API-10**: `POST /v1/files/{id}/share` generates tokenised share URL with TTL and optional password
- [ ] **API-11**: `GET /v1/categories` and `GET /v1/categories/{id}` expose `FileCategory` policy to consumers
- [ ] **API-12**: `/health` endpoint returns 200 when all dependencies reachable; 503 otherwise
- [ ] **API-13**: All endpoints validate `Authorization: Bearer` JWT; return 401 on missing/expired; propagate `traceparent` to event publishing
- [ ] **API-14**: `Idempotency-Key` header honoured on all POST endpoints
- [ ] **API-15**: Per-tenant rate limiting returns 429 with `Retry-After` header; one tenant's burst does not block others
- [ ] **API-16**: OpenAPI spec at `/openapi/v1.json` is well-formed and matches all implemented endpoints

### Client SDK

- [ ] **SDK-01**: `Storage.Sdk` NuGet project exposes `IStorageClient` with `InitiateUploadAsync`, `CompleteUploadAsync`, `GetFileAsync`, `GetDownloadUrlAsync`, `OpenReadStreamAsync`
- [ ] **SDK-02**: SDK handles retry-with-exponential-backoff, idempotency-key generation, and `Idempotency-Key` header injection
- [ ] **SDK-03**: SDK handles multipart upload chunking transparently; caller provides `Stream`, SDK breaks into parts and uploads in parallel
- [ ] **SDK-04**: SHA-256 checksum is computed client-side and sent in `CompleteUploadAsync`

### Documents Sample Microservice

- [ ] **DOCS-01**: `Documents.Api` project exists in `backend/src/` with `Document` entity, `DocumentsService`, `DocumentsRepository`, and `FileReadyHandler` event consumer
- [ ] **DOCS-02**: `POST /api/documents` initiates upload by calling Storage SDK; creates `Documents` row with `Status=uploading`; returns `{ documentId, uploadUrl, expiresAt }`
- [ ] **DOCS-03**: `POST /api/documents/{id}/confirm` calls Storage SDK `CompleteUploadAsync` with caller-supplied checksum
- [ ] **DOCS-04**: `FileReadyHandler` receives `file.ready` event; filters by `OwnerService="documents-service"`; updates `Documents.Status=ready`
- [ ] **DOCS-05**: `GET /api/documents/{id}` returns document metadata + short-lived download URL from Storage SDK
- [ ] **DOCS-06**: `GET /api/documents?projectId={id}` returns paginated document list for a project

### Profile Sample Microservice

- [ ] **PROF-01**: `Profile.Api` project exists in `backend/src/` with `Profile` entity, `ProfileService`, `ProfileRepository`, and `AvatarThumbnailWorker`
- [ ] **PROF-02**: `POST /api/profiles/{userId}/avatar` initiates avatar upload under category `avatar`; returns presigned upload URL
- [ ] **PROF-03**: `AvatarThumbnailWorker` handles `file.uploaded` for `OwnerService="profile-service"` and `Tags["kind"]="avatar"`; generates 256×256 and 64×64 thumbnails via ImageSharp; uploads each as new file; updates `Profiles` table with CDN URLs
- [ ] **PROF-04**: `GET /api/profiles/{userId}` returns profile with avatar thumbnail CDN URLs

### Angular Frontend

- [ ] **FE-01**: Angular workspace in `frontend/` uses standalone components; `projects/shared-lib` exports `FileUploaderComponent` and upload services; `projects/demo-app` is the demo shell
- [ ] **FE-02**: `StorageService` (Angular) fetches `GET /v1/categories/{id}` on component init and caches the policy; uses it to set `accept` attribute, choose upload path, and show/hide features
- [ ] **FE-03**: Small-file `UploadService` executes the three-step flow (initiate → PUT to presigned URL → confirm) as Promises; reports progress via EventEmitter
- [ ] **FE-04**: Large-file `LargeFileUploadService` executes multipart flow with configurable concurrency (default 4), progress Observable, pause/resume, and cancel (aborts in-flight requests + calls backend abort)
- [ ] **FE-05**: Upload resume state persisted in IndexedDB keyed by `SHA-256(first-MB + size)`; reconciled against `GET /v1/files/{id}/parts` on resume
- [ ] **FE-06**: `FileUploaderComponent` is a standalone Angular component accepting `category`, `initiateEndpoint`, `context`, `multiple`, `showPreview`, `crop`, `cropAspect` inputs and emitting `started`, `progress`, `uploaded`, `failed` outputs
- [ ] **FE-07**: When `crop=true` and MIME is `image/*`, the component loads ngx-image-cropper; only the cropped blob is uploaded
- [ ] **FE-08**: Angular Documents feature: upload form using `FileUploaderComponent`, document list with download links; integrates with Documents API
- [ ] **FE-09**: Angular Profile feature: avatar uploader with `crop=true cropAspect=1`; displays 256px and 64px thumbnails after upload; integrates with Profile API

### Unit Tests

- [ ] **TEST-U01**: `Storage.Domain.Tests` covers all state machine transitions, FileCategory validation rules, StorageKey format enforcement, Checksum normalisation
- [ ] **TEST-U02**: `Storage.Application.Tests` covers all `UploadService`, `DownloadService`, `FileManagementService` use cases with NSubstitute mocks for all ports — all test cases from §15.3
- [ ] **TEST-U03**: Application-layer test coverage meets 80% gate enforced in CI
- [ ] **TEST-U04**: `Documents.Api.Tests` covers DocumentsService and FileReadyHandler with mocked Storage SDK
- [ ] **TEST-U05**: `Profile.Api.Tests` covers ProfileService and AvatarThumbnailWorker with mocked Storage SDK and ImageSharp

### Integration Tests

- [ ] **TEST-I01**: `Storage.Infrastructure.Persistence.SqlServer.Tests` — all test cases from §15.4 including migrations, CRUD, concurrency, soft-delete filter
- [ ] **TEST-I02**: `Storage.Infrastructure.Storage.Wasabi.Tests` — all storage adapter test cases from §15.4 (put/get roundtrip, presigned URL TTL, multipart, range request)
- [ ] **TEST-I03**: `Storage.Infrastructure.Storage.AzureBlob.Tests` — same test cases via Azurite emulator
- [ ] **TEST-I04**: `Storage.Infrastructure.Storage.FileSystem.Tests` — capabilities-aware subset (no presigned URLs)
- [ ] **TEST-I05**: `Storage.Infrastructure.Cache.Redis.Tests` — all cache test cases from §15.4 via Testcontainers Redis
- [ ] **TEST-I06**: `Storage.Infrastructure.Messaging.RabbitMQ.Tests` — all messaging test cases from §15.4 via Testcontainers RabbitMQ
- [ ] **TEST-I07**: `Storage.Contract.Tests` — cross-adapter theory suite for `IFileStorageProvider`, `ICacheProvider`, `IEventBus`, `IUnitOfWork` per §15.6

### API Tests

- [ ] **TEST-A01**: `Storage.Api.Tests` — all HTTP test cases from §15.5 via `WebApplicationFactory` with Testcontainers SQL Server, in-memory cache, in-memory MassTransit

### E2E and Security Tests

- [ ] **TEST-E01**: Playwright browser flows from §15.7 (document upload end-to-end, avatar with thumbnails, quarantine UI, permission denial UX)
- [ ] **TEST-E02**: API E2E scenarios from §15.7 (multipart 1 GB, presigned URL TTL, cross-service event flow, multi-tenant isolation, idempotency)
- [ ] **TEST-S01**: Security test cases from §15.9 (presigned URL replay, path traversal, cross-tenant enumeration, rate limiting, JWT tampering)
- [ ] **TEST-S02**: Test fixtures from §15.10 committed under `tests/fixtures/` (valid PDF, avatar images, EICAR test signature, polyglot file, Arabic filename)

## v2 Requirements

### AI Content Validation

- **AI-01**: `IAiValidationProvider` port and `OpenAiValidationProvider`, `AzureAiValidationProvider`, `ClaudeValidationProvider` adapters implemented
- **AI-02**: `AiValidationWorker` processes `file.scanned` events for categories with `RequiresAiValidation=true`
- **AI-03**: Human-review queue and admin UI for low-confidence verdicts

### Infrastructure as Code

- **IaC-01**: Terraform modules for all Azure resources from §11.1
- **IaC-02**: AKS Helm chart with HPA configuration
- **IaC-03**: Environment-specific tfvars for staging and production

### CI/CD

- **CICD-01**: Azure DevOps YAML pipeline with unit → integration → API → code coverage stages
- **CICD-02**: Release pipeline with Terraform plan/apply, AKS deploy, migration job, smoke tests

### Performance Tests

- **PERF-01**: k6 sustained-load scenario (100 RPS upload-init, 500 RPS metadata read for 30 minutes)
- **PERF-02**: Burst scenario (1000 concurrent upload-initiations for 60 seconds)
- **PERF-03**: NBomber .NET-native scenario for large-file storm

## Out of Scope

| Feature | Reason |
|---------|--------|
| Azure provisioning (Terraform) | Infrastructure-as-code deferred to v2; local Docker Compose is the delivery target |
| Azure DevOps CI/CD pipelines | Pipeline YAML deferred; local `dotnet test` + Docker Compose sufficient for v1 |
| k6 performance load tests | Tooling and baseline data require a running staging environment |
| Mobile app | Web-first Angular demo covers the UX surface |
| Real-time WebSocket status updates | Polling via `GET /v1/files/{id}` is sufficient for v1 demo |
| Multi-region Azure deployment | Single-region demo; DR and geo-replication are architecture decisions already documented |
| Uppy integration | Architecture doc notes it as an alternative; v1 implements the custom Observable-based service to demonstrate the pattern |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| SETUP-01 – SETUP-05 | Phase 1 | Pending |
| DOMAIN-01 – DOMAIN-06 | Phase 1 | Pending |
| APP-01 – APP-07 | Phase 2 | Pending |
| PERSIST-01 – PERSIST-05 | Phase 3 | Pending |
| STORE-01 – STORE-06 | Phase 4 | Pending |
| CACHE-01 – CACHE-05 | Phase 5 | Pending |
| MSG-01 – MSG-05 | Phase 5 | Pending |
| API-01 – API-16 | Phase 6 | Pending |
| SDK-01 – SDK-04 | Phase 7 | Pending |
| DOCS-01 – DOCS-06 | Phase 7 | Pending |
| PROF-01 – PROF-04 | Phase 7 | Pending |
| FE-01 – FE-09 | Phase 8 | Pending |
| TEST-U01 – TEST-U05 | Phase 9 | Pending |
| TEST-I01 – TEST-I07 | Phase 9 | Pending |
| TEST-A01 | Phase 9 | Pending |
| TEST-E01 – TEST-E02 | Phase 10 | Pending |
| TEST-S01 – TEST-S02 | Phase 10 | Pending |

**Coverage:**
- v1 requirements: 95 total
- Mapped to phases: 95
- Unmapped: 0 ✓

---
*Requirements defined: 2026-05-15*
*Last updated: 2026-05-15 after roadmap creation (corrected count: 75 → 95)*
