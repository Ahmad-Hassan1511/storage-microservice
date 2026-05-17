# Storage Microservice

A production-grade, cloud-native **Storage Microservice** built on **.NET 10** and **Angular 17** using the **Ports and Adapters (Hexagonal Architecture)** pattern. Consuming microservices never proxy file bytes — they request a pre-signed URL from this service and clients upload/download directly to the object store.

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                        Clients / APIs                        │
└────────────────────────┬────────────────────────────────────┘
                         │
                ┌────────▼────────┐
                │  Ocelot Gateway  │  :5000
                └────────┬────────┘
                         │
                ┌────────▼────────┐
                │   Storage API   │  :5170  (Minimal APIs, JWT auth)
                └────────┬────────┘
          ┌──────────────┼──────────────┐
          │              │              │
   ┌──────▼──────┐ ┌─────▼─────┐ ┌────▼──────┐
   │  SQL Server  │ │   Redis   │ │ RabbitMQ  │
   │  (EF Core)  │ │  (Cache)  │ │(MassTransit│
   └─────────────┘ └───────────┘ └───────────┘
          │
   ┌──────▼──────────────────────┐
   │      Object Store           │
   │  Wasabi / Azure Blob /      │
   │  FileSystem (configurable)  │
   └─────────────────────────────┘
```

**Stack:** .NET 10 · ASP.NET Core Minimal APIs · EF Core 10 · Angular 17 · SQL Server · Redis · RabbitMQ · MassTransit · Ocelot · Keycloak · ClamAV

---

## Screenshots

### Documents Page
![Documents Page](docs/images/documents.png)

### Profile Page
![Profile Page](docs/images/profile.png)

### API Reference (Scalar)

| Storage API | Documents Service | Profile Service |
|---|---|---|
| ![Storage API](docs/images/storage-api.png) | ![Documents API](docs/images/documents-api.png) | ![Profile API](docs/images/profile-api.png) |

---

## Project Structure

```
.
├── docs/
│   ├── images/                                          # Architecture diagrams and screenshots
│   ├── storage-microservice-architecture.md             # Canonical architecture reference
│   ├── storage-microservice-design.docx                 # Full design document
│   └── storage-microservice-summary.docx                # Summary document
├── backend/
│   ├── src/
│   │   ├── Storage.Domain/                              # Entities, value objects, domain events
│   │   ├── Storage.Application/                         # Use cases, DTOs, port interfaces
│   │   ├── Storage.Infrastructure.Persistence.SqlServer/ # EF Core, migrations, repositories
│   │   ├── Storage.Infrastructure.Storage.Wasabi/       # S3-compatible object store adapter
│   │   ├── Storage.Infrastructure.Storage.AzureBlob/    # Azure Blob Storage adapter
│   │   ├── Storage.Infrastructure.Storage.FileSystem/   # Local filesystem adapter (dev/test)
│   │   ├── Storage.Infrastructure.Cache.Redis/          # Redis cache adapter
│   │   ├── Storage.Infrastructure.Cache.InMemory/       # In-memory cache adapter (tests)
│   │   ├── Storage.Infrastructure.Messaging.RabbitMQ/   # RabbitMQ via MassTransit
│   │   ├── Storage.Infrastructure.Messaging.AzureServiceBus/ # ASB via MassTransit
│   │   ├── Storage.Sdk/                                 # Typed HTTP client (NuGet-ready)
│   │   ├── Storage.Api/                                 # Minimal API host, DI wiring, OpenAPI
│   │   └── Storage.Gateway/                             # Ocelot reverse proxy
│   ├── samples/
│   │   ├── Documents/                                   # Sample: Documents consumer microservice
│   │   └── Profile/                                     # Sample: Profile + avatar upload
│   └── tests/
│       ├── Storage.Domain.Tests/
│       ├── Storage.Application.Tests/
│       ├── Storage.Infrastructure.Persistence.SqlServer.Tests/
│       └── Storage.Api.Tests/                           # WebApplicationFactory API tests
├── frontend/
│   └── projects/
│       ├── shared-lib/                                  # FileUploaderComponent (initiateUrl-based)
│       └── demo-app/                                    # Documents + Profile feature modules
└── e2e/                                                 # Playwright E2E & security tests
```

---

## Key Design Decisions

| Decision | Why |
|---|---|
| Single `POST /v1/files` endpoint driven by `category` | Centralises validation; no per-type endpoint proliferation |
| Pre-signed URLs as default byte transfer | Service stays stateless; never proxies bandwidth |
| `StorageCapabilities.SupportsPresignedUploadUrls` is the single branch point | FileSystem adapter transparently falls back to proxy upload |
| **Frontend never calls Storage API directly** | All uploads go through domain services (Documents, Profile) which call `Storage.Sdk` server-side — browser has no knowledge of Storage API (§12.3) |
| MassTransit over raw broker clients | RabbitMQ ↔ Azure Service Bus is a config change, zero code change |
| `Storage.Domain` and `Storage.Application` have zero NuGet dependencies | Architecture constraint: core must never reference infrastructure |
| Each sample service has its own SQLite database | True microservice isolation — no shared data store |
| Testcontainers for integration tests | Real SQL Server / Redis / RabbitMQ in CI; no manual setup |

---

## File Status Lifecycle

```
pending  ──►  scanning  ──►  ready
                        └──►  quarantined
ready    ──►  deleted
```

Files are never served until `Status = ready`. ClamAV scans happen asynchronously after `POST /v1/files/{id}/complete`.

---

## REST API

All endpoints versioned at `/v1`, authenticated with `Authorization: Bearer <jwt>`.

| Method | Path | Purpose |
|---|---|---|
| `GET` | `/health` | Health check (no auth) |
| `POST` | `/v1/files` | Initiate upload — returns `{ fileId, uploadUrl, uploadHeaders, expiresAt, proxyRequired }` |
| `PUT` | `/v1/files/{id}` | Proxy upload (when `proxyRequired = true`, e.g. filesystem adapter) |
| `POST` | `/v1/files/{id}/complete` | Confirm upload + trigger antivirus scan |
| `GET` | `/v1/files/{id}` | Metadata + short-lived presigned `downloadUrl` |
| `GET` | `/v1/files/{id}/content` | Audited proxy download (exception path) |
| `DELETE` | `/v1/files/{id}` | Soft-delete file |
| `GET` | `/v1/categories` | List all `FileCategory` policies |
| `GET` | `/openapi/v1.json` | OpenAPI 3 spec (Development only) |

Common headers: `Authorization: Bearer <jwt>` · `Idempotency-Key` · `X-Tenant-Id` · `traceparent`

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for SQL Server, Redis, RabbitMQ, Keycloak, ClamAV)
- [Node.js 20+](https://nodejs.org/) (for Angular frontend and E2E tests)

> **Important:** Before running, update the connection strings in `backend/src/Storage.Api/appsettings.json` (or `appsettings.Development.json`) to match your environment:
> - `ConnectionStrings:DefaultConnection` — SQL Server connection string
> - `ConnectionStrings:Redis` — Redis connection string (if using Redis cache)
> - `Storage:FileSystem:RootPath` — local path for file storage in dev
> - `Auth:Authority` — leave empty to use the built-in dev JWT (HS256) for local development

### 1. Start the Infrastructure

```bash
docker-compose up -d
```

This starts: SQL Server · Redis · RabbitMQ · Keycloak · ClamAV · Ocelot Gateway

### 2. Run the Storage API

```bash
cd backend/src/Storage.Api
dotnet run
# Listening on http://localhost:5170
```

The API seeds `FileCategory` rows on first boot. Check health:

```bash
curl http://localhost:5170/health
# {"status":"healthy","service":"storage-api"}
```

API reference (Scalar): http://localhost:5170/swagger/v1

### 3. Run the Sample Services (optional)

```bash
# Documents microservice — http://localhost:56163
cd backend/samples/Documents
dotnet run

# Profile microservice — http://localhost:56161
cd backend/samples/Profile
dotnet run
```

Each sample service creates its own SQLite database (`documents.db` / `profiles.db`) on first run.

### 4. Run the Angular Frontend

```bash
cd frontend
npm install
npx ng serve
# Open http://localhost:4200
```

### 5. Run E2E Tests

```bash
cd e2e
npm install
npx playwright install chromium
npx playwright test
```

> All four services must be running. Set `API_BASE_URL`, `DOCS_BASE_URL`, `PROFILE_BASE_URL`, and `APP_BASE_URL` env vars to override the default ports.

---

## Configuration

All infrastructure is selected via `appsettings.json` — **no code changes** between environments:

```jsonc
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,1433;Database=StorageDb;User Id=sa;Password=Your_Password123;TrustServerCertificate=True",
    "Redis": "localhost:6379",
    "AzureServiceBus": ""
  },
  "Storage": {
    "Provider": "filesystem",       // "wasabi" | "azureblob" | "filesystem"
    "FileSystem": { "RootPath": "C:/storage" },
    "Wasabi": { "ServiceUrl": "...", "BucketName": "...", "AccessKey": "...", "SecretKey": "..." },
    "AzureBlob": { "ConnectionString": "...", "ContainerName": "..." }
  },
  "Cache": {
    "Provider": "inmemory"          // "redis" | "inmemory"
  },
  "Messaging": {
    "Provider": "rabbitmq"          // "rabbitmq" | "azureservicebus"
  },
  "RabbitMQ": { "Uri": "amqp://guest:guest@localhost:5672" },
  "Auth": {
    "Authority": "",                // Leave empty to use dev HS256 signing key locally
    "Audience": "storage-api",
    "DevelopmentSigningKey": "dev-signing-key-32-bytes-minimum!"
  }
}
```

| Environment | Object Store | Cache | Messaging | Identity |
|---|---|---|---|---|
| Local / Dev | FileSystem | InMemory | RabbitMQ | Dev JWT (HS256) |
| Demo | Wasabi | Redis | RabbitMQ | Keycloak |
| Production | Azure Blob | Azure Cache for Redis | Azure Service Bus | Microsoft Entra ID |

---

## Running Tests

```bash
# Unit + application tests (no Docker required)
dotnet test backend/tests/Storage.Domain.Tests/Storage.Domain.Tests.csproj
dotnet test backend/tests/Storage.Application.Tests/Storage.Application.Tests.csproj

# Integration tests (requires Docker)
dotnet test backend/tests/Storage.Infrastructure.Persistence.SqlServer.Tests

# API tests via WebApplicationFactory (requires Docker for SQL Server)
dotnet test backend/tests/Storage.Api.Tests

# Build verification
dotnet build backend/StorageService.sln -warnaserror
```

Test counts: **44 domain** + **23 application** + **34 E2E** (security, lifecycle, idempotency, validation, UI flows)

---

## Storage SDK

`Storage.Sdk` is a standalone typed HTTP client — no dependency on any infrastructure project. In production, distribute it as a NuGet package; in this repo it is referenced as a project reference.

```csharp
// Register in DI (reads StorageClient:BaseUrl and StorageClient:AccessToken from config)
services.AddStorageClient(configuration);

// Use in a consumer microservice — server-side only, never from the browser
public class DocumentsController(IStorageClient storage) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> InitiateUpload([FromBody] InitiateRequest req)
    {
        var init = await storage.InitiateUploadAsync(new UploadFileRequest(
            CategoryId: "document",
            OriginalFileName: req.FileName,
            MimeType: req.MimeType,
            SizeBytes: req.SizeBytes,
            OwnerService: "documents-service"),
            idempotencyKey: Guid.NewGuid().ToString("N"));

        // Return upload coordinates to the client — never expose Storage API URL
        return Ok(new {
            proxyUploadUrl = $"/api/documents/{docId}/content",
            completeUrl    = $"/api/documents/{docId}/complete",
            init.ProxyRequired,
        });
    }
}
```

The SDK handles: idempotency key injection · SHA-256 checksum · exponential-backoff retry · presigned URL or proxy fallback.

---

## Sample Microservices

Two sample consumers demonstrate the correct integration pattern (frontend → domain service → Storage SDK → Storage API):

| Service | Port | Database | Key Endpoints |
|---|---|---|---|
| **Documents** | `:56163` | `documents.db` (SQLite) | `POST /api/documents` · `PUT /api/documents/{id}/content` · `POST /api/documents/{id}/complete` · `GET /api/documents/{id}/download` |
| **Profile** | `:56161` | `profiles.db` (SQLite) | `POST /api/profiles/me/avatar` · `PUT /api/profiles/me/avatar/{fileId}/content` · `POST /api/profiles/me/avatar/{fileId}/complete` · `GET /api/profiles/me/avatar/content` |

API reference for each service is available at `/swagger/v1`.

---

## Implementation Phases

| Phase | Description | Status |
|---|---|---|
| 1 | Solution scaffold, domain model, Docker Compose | ✅ |
| 2 | Application layer, port interfaces, use-case services | ✅ |
| 3 | EF Core SQL Server persistence adapter, migrations, soft-delete | ✅ |
| 4 | Wasabi / Azure Blob / FileSystem storage adapters | ✅ |
| 5 | Redis / InMemory cache · RabbitMQ / Azure Service Bus messaging | ✅ |
| 6 | REST API — endpoints, JWT auth, OpenAPI, config-driven DI | ✅ |
| 7 | Storage.Sdk typed client · Documents + Profile sample services | ✅ |
| 8 | Angular shared-lib `FileUploaderComponent` · feature modules | ✅ |
| 9 | Unit, integration & API tests (WebApplicationFactory) | ✅ |
| 10 | Playwright E2E + security tests · architecture enforcement | ✅ |

---

## License

MIT
