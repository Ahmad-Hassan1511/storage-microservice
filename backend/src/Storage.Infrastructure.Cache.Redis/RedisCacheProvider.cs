using System.Text.Json;
using StackExchange.Redis;
using Storage.Application.Abstractions;

namespace Storage.Infrastructure.Cache.Redis;

public sealed class RedisCacheProvider : ICacheProvider
{
    private readonly IDatabase _db;

    public RedisCacheProvider(IConnectionMultiplexer multiplexer)
    {
        _db = multiplexer.GetDatabase();
    }

    public bool IsDistributed => true;

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct)
    {
        var value = await _db.StringGetAsync(key).ConfigureAwait(false);
        if (value.IsNullOrEmpty)
            return default;
        return JsonSerializer.Deserialize<T>((string)value!);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? ttl, CancellationToken ct)
    {
        var serialized = JsonSerializer.Serialize(value);
        if (ttl.HasValue)
            await _db.StringSetAsync(key, serialized, ttl.Value).ConfigureAwait(false);
        else
            await _db.StringSetAsync(key, serialized).ConfigureAwait(false);
    }

    public async Task RemoveAsync(string key, CancellationToken ct)
    {
        await _db.KeyDeleteAsync(key).ConfigureAwait(false);
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct)
    {
        return await _db.KeyExistsAsync(key).ConfigureAwait(false);
    }
}
