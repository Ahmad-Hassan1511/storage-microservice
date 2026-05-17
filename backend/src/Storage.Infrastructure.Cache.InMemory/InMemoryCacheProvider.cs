using Microsoft.Extensions.Caching.Memory;
using Storage.Application.Abstractions;

namespace Storage.Infrastructure.Cache.InMemory;

public sealed class InMemoryCacheProvider : ICacheProvider
{
    private readonly IMemoryCache _cache;

    public InMemoryCacheProvider(IMemoryCache cache)
    {
        _cache = cache;
    }

    public bool IsDistributed => false;

    public Task<T?> GetAsync<T>(string key, CancellationToken ct)
    {
        _cache.TryGetValue(key, out T? value);
        return Task.FromResult(value);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? ttl, CancellationToken ct)
    {
        if (ttl.HasValue)
            _cache.Set(key, value, ttl.Value);
        else
            _cache.Set(key, value);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct)
    {
        _cache.Remove(key);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken ct)
    {
        return Task.FromResult(_cache.TryGetValue(key, out _));
    }
}
