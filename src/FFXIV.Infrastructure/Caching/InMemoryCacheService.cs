using FFXIV.Domain.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace FFXIV.Infrastructure.Caching;

public class InMemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;
    private readonly HashSet<string> _keys = [];
    private readonly Lock _lock = new();

    public InMemoryCacheService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        _cache.TryGetValue(key, out T? value);
        return Task.FromResult(value);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default) where T : class
    {
        var options = new MemoryCacheEntryOptions();
        if (expiration.HasValue)
            options.AbsoluteExpirationRelativeToNow = expiration;

        _cache.Set(key, value, options);

        lock (_lock)
        {
            _keys.Add(key);
        }

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        _cache.Remove(key);

        lock (_lock)
        {
            _keys.Remove(key);
        }

        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        List<string> keysToRemove;

        lock (_lock)
        {
            keysToRemove = _keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList();
        }

        foreach (var key in keysToRemove)
        {
            _cache.Remove(key);
        }

        lock (_lock)
        {
            foreach (var key in keysToRemove)
            {
                _keys.Remove(key);
            }
        }

        return Task.CompletedTask;
    }
}
