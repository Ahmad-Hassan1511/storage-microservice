---
phase: 5
plan: cache-messaging-adapters
subsystem: infrastructure
tags: [cache, redis, in-memory, messaging, rabbitmq, azure-service-bus, masstransit]
key-files:
  created:
    - backend/src/Storage.Infrastructure.Cache.Redis/RedisCacheProvider.cs
    - backend/src/Storage.Infrastructure.Cache.Redis/ServiceCollectionExtensions.cs
    - backend/src/Storage.Infrastructure.Cache.Redis/Storage.Infrastructure.Cache.Redis.csproj
    - backend/src/Storage.Infrastructure.Cache.InMemory/InMemoryCacheProvider.cs
    - backend/src/Storage.Infrastructure.Cache.InMemory/ServiceCollectionExtensions.cs
    - backend/src/Storage.Infrastructure.Cache.InMemory/Storage.Infrastructure.Cache.InMemory.csproj
    - backend/src/Storage.Infrastructure.Messaging.RabbitMQ/RabbitMqEventBus.cs
    - backend/src/Storage.Infrastructure.Messaging.RabbitMQ/ServiceCollectionExtensions.cs
    - backend/src/Storage.Infrastructure.Messaging.RabbitMQ/Storage.Infrastructure.Messaging.RabbitMQ.csproj
    - backend/src/Storage.Infrastructure.Messaging.AzureServiceBus/AzureServiceBusEventBus.cs
    - backend/src/Storage.Infrastructure.Messaging.AzureServiceBus/ServiceCollectionExtensions.cs
    - backend/src/Storage.Infrastructure.Messaging.AzureServiceBus/Storage.Infrastructure.Messaging.AzureServiceBus.csproj
decisions:
  - RedisCacheProvider branches StringSetAsync on ttl.HasValue to avoid Expiration type ambiguity
  - SubscribeAsync is a no-op in both MassTransit adapters; consumers register at DI startup via ConfigureEndpoints
  - MassTransit 8.x used for both RabbitMQ and Azure Service Bus to keep IEventBus adapter code identical
metrics:
  completed: 2026-05-16
---

# Phase 5: Cache & Messaging Adapters Summary

Four infrastructure adapters implementing ICacheProvider and IEventBus with Redis, IMemoryCache, MassTransit+RabbitMQ, and MassTransit+Azure Service Bus.

## Completed Work

### Cache Adapters

**RedisCacheProvider** (`Storage.Infrastructure.Cache.Redis`)
- Implements `ICacheProvider` backed by `IConnectionMultiplexer` (StackExchange.Redis)
- `IsDistributed = true` — safe for idempotency keys, distributed locks, rate-limit counters
- Serializes values with `System.Text.Json`; branches `StringSetAsync` on `ttl.HasValue` to use the correct `TimeSpan` overload
- `AddRedisCacheProvider(string connectionString)` DI extension

**InMemoryCacheProvider** (`Storage.Infrastructure.Cache.InMemory`)
- Implements `ICacheProvider` backed by `IMemoryCache`
- `IsDistributed = false` — application falls back to SQL Server for distributed guarantees
- No serialization overhead; stores typed objects directly
- `AddInMemoryCacheProvider()` DI extension

### Messaging Adapters

**RabbitMqEventBus** (`Storage.Infrastructure.Messaging.RabbitMQ`)
- Implements `IEventBus` via MassTransit 8.x `IPublishEndpoint`
- `PublishAsync<TEvent>` delegates to `IPublishEndpoint.Publish`
- `SubscribeAsync` is a no-op; consumer registration happens at DI startup via `ConfigureEndpoints`
- `AddRabbitMqMessaging(IConfiguration config)` reads `RabbitMQ:Uri` config key

**AzureServiceBusEventBus** (`Storage.Infrastructure.Messaging.AzureServiceBus`)
- Same pattern as RabbitMQ adapter; uses `x.UsingAzureServiceBus`
- `AddAzureServiceBusMessaging(string connectionString)` DI extension

## Build Verification

```
dotnet build backend/StorageService.sln -warnaserror
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

All 15 projects in the solution compiled successfully.

## Deviations from Plan

**[Rule 1 - Bug] Fixed StringSetAsync Expiration overload ambiguity**
- Found during: Redis adapter implementation
- Issue: `StringSetAsync(key, value, TimeSpan?)` was ambiguous with `Expiration`-typed overload in StackExchange.Redis 2.x
- Fix: Branched on `ttl.HasValue` — calls `StringSetAsync(key, value, ttl.Value)` when present, bare `StringSetAsync(key, value)` otherwise
- Files modified: `RedisCacheProvider.cs`
- Commit: 0c055e9

**[Rule 1 - Bug] Fixed JsonSerializer.Deserialize ambiguity**
- Found during: Redis adapter implementation
- Issue: `JsonSerializer.Deserialize<T>(value!)` was ambiguous between `ReadOnlySpan<byte>` and `string` overloads
- Fix: Explicit cast `(string)value!` to select string overload
- Files modified: `RedisCacheProvider.cs`
- Commit: 0c055e9

## Self-Check: PASSED

- `backend/src/Storage.Infrastructure.Cache.Redis/RedisCacheProvider.cs` — FOUND
- `backend/src/Storage.Infrastructure.Cache.InMemory/InMemoryCacheProvider.cs` — FOUND
- `backend/src/Storage.Infrastructure.Messaging.RabbitMQ/RabbitMqEventBus.cs` — FOUND
- `backend/src/Storage.Infrastructure.Messaging.AzureServiceBus/AzureServiceBusEventBus.cs` — FOUND
- Commit 0c055e9 — FOUND
