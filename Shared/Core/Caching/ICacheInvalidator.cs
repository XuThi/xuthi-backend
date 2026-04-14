namespace Core.Caching;

/// <summary>
/// Evicts cached entries by prefix.
/// Mutation handlers call this after successful DB writes.
/// </summary>
public interface ICacheInvalidator
{
    /// <summary>
    /// Remove all cache entries whose keys start with any of the given prefixes.
    /// </summary>
    void Invalidate(params string[] prefixes);

    /// <summary>
    /// Register a cache key so it can be found during invalidation.
    /// Called automatically by the caching infrastructure.
    /// </summary>
    void TrackKey(string key);
}
