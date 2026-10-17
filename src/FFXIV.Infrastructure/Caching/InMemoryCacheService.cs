using System.Text.Json;
using FFXIV.Domain.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace FFXIV.Infrastructure.Caching;

/// <summary>
/// Process-local cache with a byte budget. Every entry is sized from its JSON length, so the cache
/// compacts (least recently used first) instead of growing for the life of the process, and
/// prefix removal walks the cache's own key list rather than a shadow set that could leak.
/// </summary>
public sealed class InMemoryCacheService : ICacheService, IDisposable
{
    public const long DefaultSizeLimitBytes = 64L * 1024 * 1024;

    /// <summary>Charged per entry on top of the payload for the key and bookkeeping.</summary>
    private const int EntryOverheadBytes = 256;

    /// <summary>Used when a value cannot be serialized to measure it.</summary>
    private const int FallbackSizeBytes = 4 * 1024;

    private readonly MemoryCache _cache;
    private readonly long _sizeLimitBytes;

    public InMemoryCacheService() : this(DefaultSizeLimitBytes)
    {
    }

    public InMemoryCacheService(long sizeLimitBytes)
    {
        _sizeLimitBytes = sizeLimitBytes;
        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = sizeLimitBytes });
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        _cache.TryGetValue(key, out T? value);
        return Task.FromResult(value);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default) where T : class
    {
        var size = EstimateSize(key, value);
        if (size > _sizeLimitBytes)
        {
            // Larger than the whole budget: caching it would evict everything else for one entry.
            _cache.Remove(key);
            return Task.CompletedTask;
        }

        var options = new MemoryCacheEntryOptions { Size = size };
        if (expiration.HasValue)
            options.AbsoluteExpirationRelativeToNow = expiration;

        _cache.Set(key, value, options);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        _cache.Remove(key);
        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        var keysToRemove = _cache.Keys
            .OfType<string>()
            .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
            .ToList();

        foreach (var key in keysToRemove)
            _cache.Remove(key);

        return Task.CompletedTask;
    }

    public void Dispose() => _cache.Dispose();

    private static long EstimateSize<T>(string key, T value)
    {
        long payload;
        try
        {
            payload = JsonSerializer.SerializeToUtf8Bytes(value).Length;
        }
        catch (Exception ex) when (ex is NotSupportedException or JsonException)
        {
            payload = FallbackSizeBytes;
        }

        return payload + key.Length * sizeof(char) + EntryOverheadBytes;
    }
}
