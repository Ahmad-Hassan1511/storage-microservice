---
phase: 4
plan: storage-adapters
subsystem: storage
tags: [storage, s3, wasabi, azure-blob, filesystem, hexagonal]
requires: [Storage.Application, IFileStorageProvider]
provides: [WasabiStorageProvider, AzureBlobStorageProvider, FileSystemStorageProvider]
affects: [Storage.Api]
tech-stack-added: [AWSSDK.S3 3.7.*, Azure.Storage.Blobs 12.*]
key-files-created:
  - backend/src/Storage.Infrastructure.Storage.Wasabi/WasabiStorageProvider.cs
  - backend/src/Storage.Infrastructure.Storage.Wasabi/WasabiServiceCollectionExtensions.cs
  - backend/src/Storage.Infrastructure.Storage.AzureBlob/AzureBlobStorageProvider.cs
  - backend/src/Storage.Infrastructure.Storage.AzureBlob/AzureBlobServiceCollectionExtensions.cs
  - backend/src/Storage.Infrastructure.Storage.FileSystem/FileSystemStorageProvider.cs
  - backend/src/Storage.Infrastructure.Storage.FileSystem/FileSystemServiceCollectionExtensions.cs
key-files-modified:
  - backend/src/Storage.Infrastructure.Storage.Wasabi/Storage.Infrastructure.Storage.Wasabi.csproj
  - backend/src/Storage.Infrastructure.Storage.AzureBlob/Storage.Infrastructure.Storage.AzureBlob.csproj
  - backend/src/Storage.Infrastructure.Storage.FileSystem/Storage.Infrastructure.Storage.FileSystem.csproj
  - backend/src/Storage.Infrastructure.Cache.Redis/RedisCacheProvider.cs
decisions:
  - "FileSystem adapter returns ProxyRequired=true and null PresignedUrl — application layer branches on Capabilities.SupportsPresignedUploadUrls"
  - "Range support in FileSystem uses bounded MemoryStream (avoids custom stream class); Wasabi uses ByteRange on GetObjectRequest; AzureBlob uses HttpRange on BlobDownloadOptions"
  - "Path traversal guard added to FileSystem ResolvePath — resolves full path and validates it stays under BasePath"
  - "Provider selection (Storage:Provider config key) deferred to Phase 6 API wiring; each adapter exposes AddXxxStorage() extension"
metrics:
  duration: ~15min
  completed: 2026-05-16
  tasks: 3 adapters + 1 pre-existing bug fix
  files: 9 created/modified
---

# Phase 4: Storage Adapters Summary

All three `IFileStorageProvider` adapters implemented with full 7-method contract compliance and HTTP Range support. Solution builds with 0 warnings, 0 errors (`-warnaserror`).

## What Was Built

### WasabiStorageProvider
- Pre-signed PUT URL via `AmazonS3Client.GetPreSignedURLAsync` (Verb.PUT)
- Pre-signed GET URL via `AmazonS3Client.GetPreSignedURLAsync` (Verb.GET)
- `OpenReadStreamAsync` passes `ByteRange` header to S3 for native Range support
- `WriteStreamAsync` uses `PutObjectRequest` with `AutoCloseStream=false`
- `Capabilities`: `SupportsPresignedUploadUrls=true`, `SupportsPresignedDownloadUrls=true`, `SupportsMultipartUpload=true`
- Config: `Storage:Wasabi:ServiceUrl`, `AccessKey`, `SecretKey`, `Region`, `ForcePathStyle`

### AzureBlobStorageProvider
- SAS URI for upload via `BlobClient.GenerateSasUri(BlobSasPermissions.Write|Create, expiresOn)`
- SAS URI for download via `BlobClient.GenerateSasUri(BlobSasPermissions.Read, expiresOn)`
- `OpenReadStreamAsync` uses `BlobDownloadOptions { Range = new HttpRange(from, length) }`
- `WriteStreamAsync` uses `BlobClient.UploadAsync` with `BlobHttpHeaders`
- `Capabilities`: `SupportsPresignedUploadUrls=true`, `SupportsPresignedDownloadUrls=true`
- Config: `Storage:AzureBlob:ConnectionString`, `DefaultContainerName`

### FileSystemStorageProvider
- `GetUploadTargetAsync` returns `StoragePutResult(null, null, ProxyRequired: true)`
- `GetDownloadTargetAsync` returns `StorageGetResult(null, ProxyRequired: true)`
- `OpenReadStreamAsync` seeks to Range offset and copies bounded bytes into `MemoryStream`
- Path traversal guard: resolves `Path.GetFullPath` and validates result stays under `BasePath`
- `Capabilities`: `SupportsPresignedUploadUrls=false`, `SupportsPresignedDownloadUrls=false`
- Config: `Storage:FileSystem:BasePath`

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed pre-existing RedisCacheProvider compile errors**
- Found during: build verification
- Issue: `JsonSerializer.Deserialize` ambiguous overload (RedisValue vs string), and `TimeSpan?` not implicitly convertible to `StackExchange.Redis.Expiration`
- Fix: Cast `RedisValue` to `(string)` before deserialize; split `StringSetAsync` into two overloads (with/without TTL)
- Files modified: `backend/src/Storage.Infrastructure.Cache.Redis/RedisCacheProvider.cs`

## Self-Check: PASSED

- `Storage.Infrastructure.Storage.Wasabi.dll` — built
- `Storage.Infrastructure.Storage.AzureBlob.dll` — built
- `Storage.Infrastructure.Storage.FileSystem.dll` — built
- Solution: 0 warnings, 0 errors with `-warnaserror`
