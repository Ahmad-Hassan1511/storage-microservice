# ROADMAP: Storage Microservice

**Project:** Storage Microservice
**Granularity:** Standard
**Total Phases:** 10
**v1 Requirements Mapped:** 95/95

---

## Phases

- [x] **Phase 1: Solution Scaffold & Domain Model** - Working .NET solution with project references, Docker Compose stack, and a fully validated domain core (completed 2026-05-15)
- [ ] **Phase 2: Application Layer & Port Interfaces** - All use-case services and port interfaces implemented; no infrastructure dependency
- [ ] **Phase 3: Persistence Adapter** - EF Core SQL Server adapter fulfils the persistence port with migrations, seeder, and soft-delete query filter
- [ ] **Phase 4: Storage Adapters** - FileSystem, Wasabi, and Azure Blob adapters implement the storage port; adapter selection driven by config
- [ ] **Phase 5: Cache & Messaging Adapters** - InMemory and Redis cache adapters; InMemory, RabbitMQ, and Azure Service Bus messaging adapters all wired through MassTransit
- [ ] **Phase 6: REST API Layer** - All `/v1/files` and `/v1/categories` endpoints live with auth, idempotency, rate limiting, and OpenAPI spec
- [ ] **Phase 7: Client SDK & Sample Microservices** - `Storage.Sdk` NuGet package; Documents and Profile sample services demonstrating correct integration
- [ ] **Phase 8: Angular Frontend** - Shared upload library with `FileUploaderComponent`; Documents and Profile feature modules in the demo app
- [ ] **Phase 9: Unit, Integration & API Tests** - 80% coverage gate on application layer; all adapter integration tests via Testcontainers; full HTTP surface via WebApplicationFactory
- [ ] **Phase 10: E2E & Security Tests** - Playwright browser flows, API E2E scenarios, security tests, and committed test fixtures

---

## Phase Details

### Phase 1: Solution Scaffold & Domain Model

**Goal:** The repository has a buildable solution structure and a domain core that enforces all business invariants independently of any infrastructure.

**Depends on:** Nothing (first phase)

**Requirements:** SETUP-01, SETUP-02, SETUP-03, SETUP-04, SETUP-05, DOMAIN-01, DOMAIN-02, DOMAIN-03, DOMAIN-04, DOMAIN-05, DOMAIN-06

**Success Criteria** (what must be TRUE):
  1. Running `docker-compose up` starts SQL Server, Redis, RabbitMQ, Keycloak, ClamAV, and the Storage API container; the Ocelot gateway responds on port 5000.
  2. The `Storage.Application` and `Storage.Domain` projects have no project references to any `Storage.Infrastructure.*` project; `dotnet build` passes with zero warnings on the dependency check.
  3. Attempting an invalid `File` status transition (e.g., `pending → ready`) throws a domain exception; all valid transitions (`pending → scanning → ready`, `scanning → quarantined`, `ready → deleted`) succeed.
  4. `StorageKey` rejects keys that violate the `<tenantId>/<yyyy>/<mm>/<dd>/<uuid>` format; `Checksum` rejects non-SHA-256 hex strings and normalises lowercase.
  5. All six domain events are instantiable and carry their required fields as defined in `Storage.Domain`.

**Plans:** 3/3 plans complete

Plans:
- [ ] 01-01-PLAN.md — Backend .NET solution scaffold + Angular workspace + xUnit v3 test stubs (Wave 1)
- [ ] 01-02-PLAN.md — Domain core: entities, value objects, events, state machine (Wave 2, TDD)
- [ ] 01-03-PLAN.md — Docker Compose stack + Ocelot gateway (Wave 2, has checkpoint)

---

### Phase 2: Application Layer & Port Interfaces

**Goal:** All port interfaces are defined and all use-case services are implemented against those ports; the application layer is fully exercisable with mock adapters and enforces policy, idempotency, and state transitions.

**Depends on:** Phase 1

**Requirements:** APP-01, APP-02, APP-03, APP-04, APP-05, APP-06, APP-07

**Success Criteria** (what must be TRUE):
  1. `IFileStorageProvider`, `ICacheProvider`, `IEventBus`, and `IUnitOfWork` are defined in `Storage.Application/Abstractions/`; no infrastructure assembly is referenced at compile time.
  2. `UploadService.InitiateUploadAsync` returns a structured response (fileId, uploadUrl, uploadHeaders, expiresAt, proxyRequired, multipartRequired) for a valid request; it returns an error for requests that violate category policy (size, MIME, extension, or disallowed owner service).
  3. Submitting two calls with the same `Idempotency-Key` and identical payload returns the same `FileId` both times; submitting the same key with a different payload returns a 422-equivalent domain error.
  4. `UploadService.CompleteUploadAsync` transitions file status to `scanning` and publishes `file.uploaded` when the SHA-256 checksum matches; it rejects on mismatch.
  5. `DownloadService.GetFileAsync` returns a pre-signed download URL for `ready` files the caller is authorised to access; it returns an access-denied result for cross-tenant or insufficient-permission requests.

**Plans:** 3/4 plans executed

Plans:
- [ ] 02-01-PLAN.md — Port interfaces, Result type, CallerContext, integration events, shared DTOs, test project scaffold (Wave 1)
- [ ] 02-02-PLAN.md — UploadService: InitiateUploadAsync + CompleteUploadAsync with TDD (Wave 2)
- [ ] 02-03-PLAN.md — DownloadService: GetFileAsync + GetFileStreamAsync with TDD (Wave 2)
- [ ] 02-04-PLAN.md — FileManagementService: soft delete, hard delete, list, patch, versions, share links with TDD (Wave 2)

---

### Phase 3: Persistence Adapter

**Goal:** The EF Core SQL Server adapter fully implements the persistence port; the database schema matches the canonical design, migrations run cleanly, and the soft-delete query filter is transparent to callers.

**Depends on:** Phase 2

**Requirements:** PERSIST-01, PERSIST-02, PERSIST-03, PERSIST-04, PERSIST-05

**Success Criteria** (what must be TRUE):
  1. Running `dotnet ef migrations apply` against a blank SQL Server database creates all tables, indexes, and check constraints as specified in §6.3 with no manual SQL required.
  2. The seeder inserts the starter `FileCategory` rows from §6.1 on first run; re-running the seeder does not create duplicates.
  3. Soft-deleted files are excluded from all default LINQ queries; they are retrievable only when the query explicitly opts in to include deleted rows.
  4. `EfUnitOfWork.ExecuteInTransactionAsync` wraps multi-step operations in a single database transaction; a simulated failure mid-transaction rolls back all changes.
  5. `FileRepository` correctly executes all query patterns needed by the use cases (by ID, by owner, by category, cursor-based page, concurrency-token update).

**Plans:** TBD

---

### Phase 4: Storage Adapters

**Goal:** All three storage adapters implement the `IFileStorageProvider` port; adapter selection is driven by a single configuration key; the `StorageCapabilities` flag governs all branching behaviour in the application layer.

**Depends on:** Phase 2

**Requirements:** STORE-01, STORE-02, STORE-03, STORE-04, STORE-05, STORE-06

**Success Criteria** (what must be TRUE):
  1. `FileSystemStorageProvider` stores a file to the configured root directory and reads it back correctly; `Capabilities.SupportsPresignedUploadUrls` is `false`; `StoragePutResult.ProxyRequired` is `true`.
  2. `WasabiStorageProvider` generates a pre-signed PUT URL; a test can PUT bytes directly to that URL and then retrieve them via a pre-signed GET URL without going through the API.
  3. `AzureBlobStorageProvider` generates a valid SAS URI against Azurite; a round-trip upload and download succeeds.
  4. All three adapters implement `OpenReadStreamAsync` with HTTP Range support; a request for bytes 1000–1999 of a 5000-byte file returns exactly 1000 bytes.
  5. Setting `Storage:Provider` to `filesystem`, `wasabi`, or `azureblob` in `appsettings` selects the correct adapter at startup; an unknown value causes the application to throw and refuse to start.

**Plans:** TBD

---

### Phase 5: Cache & Messaging Adapters

**Goal:** Cache and messaging adapters implement their respective ports; switching provider is a configuration change; distributed lock and dead-letter semantics work correctly.

**Depends on:** Phase 2

**Requirements:** CACHE-01, CACHE-02, CACHE-03, CACHE-04, CACHE-05, MSG-01, MSG-02, MSG-03, MSG-04, MSG-05

**Success Criteria** (what must be TRUE):
  1. `InMemoryCacheProvider.IsDistributed` is `false`; `RedisCacheProvider.IsDistributed` is `true`; contract tests for both adapters pass with identical assertions.
  2. A pre-signed URL cached under `presigned:get:{fileId}:{principal}` expires from the cache before the URL's own TTL expires; a subsequent call generates a fresh URL.
  3. Changing `EventBus:Provider` from `rabbitmq` to `azureservicebus` in `appsettings` requires no code change; all six domain events publish and are received by a registered handler under both providers.
  4. A handler that fails exhausts its retry policy with exponential backoff; the message lands in the dead-letter queue and does not block subsequent messages.
  5. `InMemoryEventBus` is usable as a drop-in replacement in tests; events published in one part of the test are received synchronously by registered handlers before the assertion runs.

**Plans:** TBD

---

### Phase 6: REST API Layer

**Goal:** The complete HTTP surface is live; every endpoint enforces authentication, idempotency, rate limiting, and returns the correct status codes; the OpenAPI spec matches the implementation.

**Depends on:** Phase 3, Phase 4, Phase 5

**Requirements:** API-01, API-02, API-03, API-04, API-05, API-06, API-07, API-08, API-09, API-10, API-11, API-12, API-13, API-14, API-15, API-16

**Success Criteria** (what must be TRUE):
  1. The full upload flow — `POST /v1/files` → PUT to presigned URL → `POST /v1/files/{id}/complete` — succeeds end-to-end for both small and multipart files; checksum mismatch on complete returns 409.
  2. `GET /v1/files/{id}` returns `downloadUrl` (and `previewUrl`/`thumbnailUrl` when present) for `ready` files; it returns 404 for cross-tenant requests and for files in `pending` or `scanning` status.
  3. All endpoints return 401 for missing or expired JWTs; `DELETE /v1/files/{id}?hard=true` returns 403 without `storage.admin` scope; `GET /v1/files` with cursor pagination returns the correct next page.
  4. A tenant that exceeds its rate limit receives 429 with a `Retry-After` header; requests from a second tenant in the same burst window are not affected.
  5. `GET /openapi/v1.json` returns a well-formed OpenAPI 3 document; every implemented endpoint appears in the spec with correct request/response schemas.

**Plans:** TBD

---

### Phase 7: Client SDK & Sample Microservices

**Goal:** `Storage.Sdk` provides a fully functional typed client with retry, idempotency, and multipart support; the Documents and Profile sample services demonstrate the correct consumer integration pattern.

**Depends on:** Phase 6

**Requirements:** SDK-01, SDK-02, SDK-03, SDK-04, DOCS-01, DOCS-02, DOCS-03, DOCS-04, DOCS-05, DOCS-06, PROF-01, PROF-02, PROF-03, PROF-04

**Success Criteria** (what must be TRUE):
  1. A caller provides a `Stream` to `IStorageClient`; the SDK transparently splits it into parts, uploads them in parallel, computes the SHA-256 checksum, and calls `CompleteUploadAsync` — the caller makes a single SDK call with no knowledge of multipart mechanics.
  2. The SDK injects an `Idempotency-Key` header on every POST and retries transient failures with exponential backoff; a duplicate request with the same key is idempotent at the server.
  3. `POST /api/documents` creates a Document row with `Status=uploading` and returns `{ documentId, uploadUrl, expiresAt }`; when the `file.ready` event is received by `FileReadyHandler`, the Document row transitions to `Status=ready`.
  4. `POST /api/profiles/{userId}/avatar` returns a pre-signed upload URL; after upload and scanning, `AvatarThumbnailWorker` uploads 256×256 and 64×64 thumbnails and `GET /api/profiles/{userId}` returns the CDN URLs for both.

**Plans:** TBD

---

### Phase 8: Angular Frontend

**Goal:** The shared upload library is complete; both Documents and Profile Angular feature modules use `FileUploaderComponent` to execute the full upload flow including large-file multipart, resume, and image crop.

**Depends on:** Phase 7

**Requirements:** FE-01, FE-02, FE-03, FE-04, FE-05, FE-06, FE-07, FE-08, FE-09

**Success Criteria** (what must be TRUE):
  1. `FileUploaderComponent` emits `started`, `progress`, `uploaded`, and `failed` events during a small-file upload; the `accept` attribute and feature toggles are driven by the category policy fetched from `GET /v1/categories/{id}`.
  2. A large-file upload (>100 MB) executes the multipart flow with 4 concurrent part uploads; pausing and resuming the upload recovers from IndexedDB state without re-uploading already completed parts.
  3. When `crop=true` and the selected file is an image, the ngx-image-cropper appears before upload; only the cropped blob is sent to the presigned URL.
  4. The Documents feature module displays the upload form and a paginated document list with working download links retrieved from the Documents API.
  5. The Profile feature module uploads an avatar with `crop=true cropAspect=1` and displays both the 256px and 64px thumbnail CDN URLs after the `AvatarThumbnailWorker` completes.

**Plans:** TBD

---

### Phase 9: Unit, Integration & API Tests

**Goal:** The application layer meets the 80% coverage gate; every adapter is validated against a real backing service via Testcontainers; the full HTTP surface is tested via WebApplicationFactory.

**Depends on:** Phase 8

**Requirements:** TEST-U01, TEST-U02, TEST-U03, TEST-U04, TEST-U05, TEST-I01, TEST-I02, TEST-I03, TEST-I04, TEST-I05, TEST-I06, TEST-I07, TEST-A01

**Success Criteria** (what must be TRUE):
  1. The application-layer coverage report (`Storage.Application.Tests`) shows >= 80% line coverage; a build configured to fail below this threshold does so when coverage is artificially reduced.
  2. All domain state machine transitions, `StorageKey` format rules, `Checksum` normalisation, and `FileCategory.Validate` rules are covered by unit tests that use no mocks or I/O.
  3. Integration test suites for Wasabi, Azurite, SQL Server, Redis, and RabbitMQ each pass with real containers spun up by Testcontainers; no external manual setup is required.
  4. The contract test suite (`Storage.Contract.Tests`) runs the same theory-parameterised test cases against all three storage adapters, both cache adapters, and both messaging adapters; all assertions are identical across implementations.
  5. `Storage.Api.Tests` exercises every HTTP endpoint via `WebApplicationFactory` with Testcontainers SQL Server and in-memory MassTransit; all documented status codes (200, 201, 400, 401, 403, 404, 409, 413, 415, 422, 429, 503) are asserted by at least one test case.

**Plans:** TBD

---

### Phase 10: E2E & Security Tests

**Goal:** Full-stack browser flows pass in Playwright; API E2E scenarios validate multi-tenant isolation, presigned URL TTL, and idempotency at the integration boundary; security tests confirm that the attack surface is locked down.

**Depends on:** Phase 9

**Requirements:** TEST-E01, TEST-E02, TEST-S01, TEST-S02

**Success Criteria** (what must be TRUE):
  1. Playwright browser flows pass for: document upload end-to-end, avatar upload with crop and thumbnail display, quarantine UI (file stuck in scanning is shown as quarantined), and permission-denial UX (403 banner on unauthorised access).
  2. The API E2E suite confirms: a 1 GB multipart upload completes successfully via the SDK; a presigned URL is rejected after its TTL expires; a `file.ready` event from Storage updates the Document status in the Documents microservice.
  3. Security tests confirm: a replayed presigned URL past TTL returns 403; a path-traversal attempt (`../etc/passwd` in filename) is rejected at the API; cross-tenant `GET /v1/files/{id}` returns 404 (not a data leak); JWT with tampered `tenantId` claim is rejected.
  4. Test fixtures (valid PDF, avatar images, EICAR test signature, polyglot file, Arabic filename) are committed under `tests/fixtures/` and are used by name in the test suites.

**Plans:** TBD

---

## Progress

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Solution Scaffold & Domain Model | 3/3 | Complete   | 2026-05-15 |
| 2. Application Layer & Port Interfaces | 3/4 | In Progress|  |
| 3. Persistence Adapter | 0/? | Not started | - |
| 4. Storage Adapters | 0/? | Not started | - |
| 5. Cache & Messaging Adapters | 0/? | Not started | - |
| 6. REST API Layer | 0/? | Not started | - |
| 7. Client SDK & Sample Microservices | 0/? | Not started | - |
| 8. Angular Frontend | 0/? | Not started | - |
| 9. Unit, Integration & API Tests | 0/? | Not started | - |
| 10. E2E & Security Tests | 0/? | Not started | - |

---

## Coverage Map

| Requirement Group | Phase | Count |
|-------------------|-------|-------|
| SETUP-01 – SETUP-05 | Phase 1 | 5 |
| DOMAIN-01 – DOMAIN-06 | Phase 1 | 6 |
| APP-01 – APP-07 | Phase 2 | 7 |
| PERSIST-01 – PERSIST-05 | Phase 3 | 5 |
| STORE-01 – STORE-06 | Phase 4 | 6 |
| CACHE-01 – CACHE-05 | Phase 5 | 5 |
| MSG-01 – MSG-05 | Phase 5 | 5 |
| API-01 – API-16 | Phase 6 | 16 |
| SDK-01 – SDK-04 | Phase 7 | 4 |
| DOCS-01 – DOCS-06 | Phase 7 | 6 |
| PROF-01 – PROF-04 | Phase 7 | 4 |
| FE-01 – FE-09 | Phase 8 | 9 |
| TEST-U01 – TEST-U05 | Phase 9 | 5 |
| TEST-I01 – TEST-I07 | Phase 9 | 7 |
| TEST-A01 | Phase 9 | 1 |
| TEST-E01 – TEST-E02 | Phase 10 | 2 |
| TEST-S01 – TEST-S02 | Phase 10 | 2 |
| **Total** | | **95** |

**Coverage: 95/95 v1 requirements mapped. No orphans.**

---

*Roadmap created: 2026-05-15*
*Last updated: 2026-05-16 after Phase 2 planning*
