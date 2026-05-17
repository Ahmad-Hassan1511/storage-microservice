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
                │   Storage API   │  :5100  (Minimal APIs, JWT auth)
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

## Project Structure

```
.
├── backend/
│   ├── src/
│   │   ├── Storage.Domain/                          # Entities, value objects, domain events
│   │   ├── Storage.Application/                     # Use cases, DTOs, port interfaces
│   │   ├── Storage.Infrastructure.Persistence.SqlServer/  # EF Core, migrations, repositories
│   │   ├── Storage.Infrastructure.Storage.Wasabi/   # S3-compatible object store adapter
│   │   ├── Storage.Infrastructure.Storage.AzureBlob/ # Azure Blob Storage adapter
│   │   ├── Storage.Infrastructure.Storage.FileSystem/ # Local filesystem adapter (dev/test)
│   │   ├── Storage.Infrastructure.Cache.Redis/       # Redis cache adapter
│   │   ├── Storage.Infrastructure.Cache.InMemory/    # In-memory cache adapter (tests)
│   │   ├── Storage.Infrastructure.Messaging.RabbitMQ/     # RabbitMQ via MassTransit
│   │   ├── Storage.Infrastructure.Messaging.AzureServiceBus/ # ASB via MassTransit
│   │   ├── Storage.Sdk/                              # Typed HTTP client (NuGet-ready)
│   │   ├── Storage.Api/                              # Minimal API host, DI wiring, OpenAPI
│   │   └── Storage.Gateway/                          # Ocelot reverse proxy
│   ├── samples/
│   │   ├── Documents/                                # Sample: Documents consumer microservice
│   │   └── Profile/                                  # Sample: Profile + avatar thumbnail worker
│   └── tests/
│       ├── Storage.Domain.Tests/
│       ├── Storage.Application.Tests/
│       ├── Storage.Infrastructure.Persistence.SqlServer.Tests/
│       └── Storage.Api.Tests/                        # WebApplicationFactory API tests
├── frontend/
│   └── projects/
│       ├── shared-lib/                               # FileUploaderComponent, FileApiService
│       └── demo-app/                                 # Documents + Profile feature modules
└── e2e/                                              # Playwright E2E & security tests
```

---

## Key Design Decisions

| Decision | Why |
|---|---|
| Single `POST /v1/files` endpoint driven by `category` | Centralises validation; no per-type endpoint proliferation |
| Pre-signed URLs as default byte transfer | Service stays stateless; never proxies bandwidth |
| `StorageCapabilities.SupportsPresignedUploadUrls` is the single branch point | FileSystem adapter transparently falls back to proxy upload |
| MassTransit over raw broker clients | RabbitMQ ↔ Azure Service Bus is a config change, zero code change |
| `Storage.Domain` and `Storage.Application` have zero NuGet dependencies | Architecture constraint: core must never reference infrastructure |
| EF Core soft-delete via `SoftDeleteInterceptor` + query filter | Deleted files invisible to default queries; hard-delete available to admins |
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
| `POST` | `/v1/files/{id}/complete` | Confirm upload + trigger antivirus scan |
| `GET` | `/v1/files/{id}` | Metadata + short-lived presigned `downloadUrl` |
| `GET` | `/v1/files/{id}/content` | Audited proxy download (exception path) |
| `GET` | `/v1/categories` | List all `FileCategory` policies |
| `GET` | `/openapi/v1.json` | OpenAPI 3 spec (Development only) |

Common headers: `Authorization: Bearer <jwt>` · `Idempotency-Key` · `X-Tenant-Id` · `traceparent`

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for SQL Server, Redis, RabbitMQ, Keycloak, ClamAV)
- [Node.js 20+](https://nodejs.org/) (for Angular frontend and E2E tests)

### 1. Start the Infrastructure

```bash
docker-compose up -d
```

This starts: SQL Server · Redis · RabbitMQ · Keycloak · ClamAV · Ocelot Gateway

### 2. Run the API

```bash
cd backend/src/Storage.Api
dotnet run --urls "http://localhost:5100"
```

The API seeds `FileCategory` rows on first boot. Check health:

```bash
curl http://localhost:5100/health
# {"status":"healthy","service":"storage-api"}
```

### 3. Run the Angular Frontend

```bash
cd frontend
npm install
npx ng serve
# Open http://localhost:4200
```

### 4. Run E2E Tests

```bash
cd e2e
npm install
npx playwright install chromium
npx playwright test
```

> **Note:** The API must be running at `http://localhost:5100` and Angular at `http://localhost:4200`.  
> Set `API_BASE_URL` and `APP_BASE_URL` env vars to override.

---

## Configuration

All infrastructure is selected via `appsettings.json` — **no code changes** between environments:

```jsonc
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,1433;Database=StorageDb;...",
    "Redis": "localhost:6379",
    "AzureServiceBus": ""
  },
  "Storage": {
    "Provider": "filesystem",       // "wasabi" | "azureblob" | "filesystem"
    "FileSystem": { "RootPath": "C:/storage" },
    "Wasabi": { "ServiceUrl": "...", "BucketName": "...", ... },
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
    "Authority": "http://localhost:8080/realms/storage",
    "Audience": "storage-api"
  }
}
```

| Environment | Object Store | Cache | Messaging | Identity |
|---|---|---|---|---|
| Local / Dev | FileSystem | InMemory | RabbitMQ | Keycloak |
| Demo | Wasabi | Redis | RabbitMQ | Keycloak |
| Production | Azure Blob | Azure Cache for Redis | Azure Service Bus | Microsoft Entra ID |

---

## Running Tests

```bash
# Unit + application tests
dotnet test backend/tests/Storage.Domain.Tests
dotnet test backend/tests/Storage.Application.Tests

# Integration tests (requires Docker)
dotnet test backend/tests/Storage.Infrastructure.Persistence.SqlServer.Tests

# API tests via WebApplicationFactory (requires Docker for SQL Server)
dotnet test backend/tests/Storage.Api.Tests

# Build verification (all 17 projects)
dotnet build backend/StorageService.sln -warnaserror
```

---

## Storage SDK

`Storage.Sdk` is a standalone typed HTTP client — no dependency on any infrastructure project:

```csharp
// Register in DI
services.AddStorageClient(configuration);

// Use in a consumer microservice
public class DocumentsService(IStorageClient storage)
{
    public async Task<Guid> UploadAsync(Stream content, string fileName)
    {
        var result = await storage.UploadAsync(content, new UploadFileRequest(
            CategoryId: "document",
            OriginalFileName: fileName,
            MimeType: "application/pdf",
            SizeBytes: content.Length,
            OwnerService: "documents-service"));

        return result.FileId;
    }
}
```

The SDK handles: idempotency key injection · SHA-256 checksum computation · exponential-backoff retry · direct PUT to presigned URL (or proxy fallback).

---

## Sample Microservices

Two sample consumers demonstrate the integration pattern:

- **Documents** (`backend/samples/Documents`) — `POST /api/documents`, subscribes to `file.ready` to transition document status
- **Profile** (`backend/samples/Profile`) — `POST /api/profiles/{userId}/avatar`, `AvatarFileReadyHandler` generates 256×256 and 64×64 WebP thumbnails via ImageSharp and re-uploads them

---

## Implementation Phases

| Phase | Description | Status |
|---|---|---|
| 1 | Solution scaffold, domain model, Docker Compose | ✅ |
| 2 | Application layer, port interfaces, use-case services | ✅ |
| 3 | EF Core SQL Server persistence adapter, migrations, soft-delete | ✅ |
| 4 | Wasabi / Azure Blob / FileSystem storage adapters | ✅ |
| 5 | Redis / InMemory cache · RabbitMQ / Azure Service Bus messaging | ✅ |
| 6 | REST API — 5 endpoints, JWT auth, OpenAPI, config-driven DI | ✅ |
| 7 | Storage.Sdk typed client · Documents + Profile sample services | ✅ |
| 8 | Angular shared-lib `FileUploaderComponent` · feature modules | ✅ |
| 9 | Unit, integration & API tests (WebApplicationFactory) | ✅ |
| 10 | Playwright E2E + security tests (10/10 passing) | ✅ |

---

## License

MIT
