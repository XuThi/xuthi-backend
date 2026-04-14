using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace Core.Caching;

/// <summary>
/// IMemoryCache wrapper that tracks keys by prefix for targeted invalidation.
/// Thread-safe via ConcurrentDictionary.
/// </summary>
public class MemoryCacheInvalidator : ICacheInvalidator
{
    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<string, byte> _trackedKeys = new();

    public MemoryCacheInvalidator(IMemoryCache cache)
    {
        _cache = cache;
    }

    public void TrackKey(string key)
    {
        _trackedKeys.TryAdd(key, 0);
    }

    public void Invalidate(params string[] prefixes)
    {
        var keysToRemove = _trackedKeys.Keys
            .Where(k => prefixes.Any(p => k.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        foreach (var key in keysToRemove)
        {
            _cache.Remove(key);
            _trackedKeys.TryRemove(key, out _);
        }
    }
}
