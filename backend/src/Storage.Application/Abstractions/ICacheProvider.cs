namespace Storage.Application.Abstractions;

public interface ICacheProvider
{
    bool IsDistributed { get; }
    Task<T?> GetAsync<T>(string key, CancellationToken ct);
    Task SetAsync<T>(string key, T value, TimeSpan? ttl, CancellationToken ct);
    Task RemoveAsync(string key, CancellationToken ct);
    Task<bool> ExistsAsync(string key, CancellationToken ct);
}
