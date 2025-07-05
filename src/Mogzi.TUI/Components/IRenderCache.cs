namespace Mogzi.TUI.Components;

/// <summary>
/// Interface for render caching to prevent unnecessary re-renders.
/// Provides dirty tracking and cached content management.
/// </summary>
public interface IRenderCache
{
    /// <summary>
    /// Checks if the cached content is dirty and needs re-rendering.
    /// </summary>
    /// <param name="key">The cache key to check</param>
    /// <returns>True if the content is dirty and needs re-rendering</returns>
    bool IsDirty(string key);

    /// <summary>
    /// Marks the cached content as dirty, requiring re-rendering.
    /// </summary>
    /// <param name="key">The cache key to mark as dirty</param>
    void MarkDirty(string key);

    /// <summary>
    /// Marks the cached content as clean after rendering.
    /// </summary>
    /// <param name="key">The cache key to mark as clean</param>
    void MarkClean(string key);

    /// <summary>
    /// Gets cached rendered content if available and not dirty.
    /// </summary>
    /// <param name="key">The cache key</param>
    /// <returns>The cached renderable content, or null if not cached or dirty</returns>
    IRenderable? GetCachedRender(string key);

    /// <summary>
    /// Caches rendered content for future use.
    /// </summary>
    /// <param name="key">The cache key</param>
    /// <param name="content">The rendered content to cache</param>
    void CacheRender(string key, IRenderable content);

    /// <summary>
    /// Invalidates all cached content, marking everything as dirty.
    /// </summary>
    void InvalidateAll();

    /// <summary>
    /// Invalidates specific cached content by key.
    /// </summary>
    /// <param name="key">The cache key to invalidate</param>
    void Invalidate(string key);
}

/// <summary>
/// Simple render cache implementation with LRU eviction.
/// Thread-safe for concurrent access.
/// </summary>
public class TuiRenderCache(int maxEntries = 100) : IRenderCache
{
    private readonly Dictionary<string, CacheEntry> _cache = [];
    private readonly Dictionary<string, bool> _dirtyFlags = [];
    private readonly Lock _lock = new();
    private readonly int _maxEntries = maxEntries;

    public bool IsDirty(string key)
    {
        lock (_lock)
        {
            return _dirtyFlags.GetValueOrDefault(key, true);
        }
    }

    public void MarkDirty(string key)
    {
        lock (_lock)
        {
            _dirtyFlags[key] = true;
        }
    }

    public void MarkClean(string key)
    {
        lock (_lock)
        {
            _dirtyFlags[key] = false;
        }
    }

    public IRenderable? GetCachedRender(string key)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var entry) && !IsDirty(key))
            {
                entry.LastAccessed = DateTime.UtcNow;
                return entry.Content;
            }
            return null;
        }
    }

    public void CacheRender(string key, IRenderable content)
    {
        lock (_lock)
        {
            // Evict oldest entries if cache is full
            if (_cache.Count >= _maxEntries)
            {
                var oldestKey = _cache
                    .OrderBy(kvp => kvp.Value.LastAccessed)
                    .First().Key;
                _ = _cache.Remove(oldestKey);
                _ = _dirtyFlags.Remove(oldestKey);
            }

            _cache[key] = new CacheEntry
            {
                Content = content,
                LastAccessed = DateTime.UtcNow
            };
            MarkClean(key);
        }
    }

    public void InvalidateAll()
    {
        lock (_lock)
        {
            _dirtyFlags.Clear();
            foreach (var key in _cache.Keys)
            {
                _dirtyFlags[key] = true;
            }
        }
    }

    public void Invalidate(string key)
    {
        lock (_lock)
        {
            MarkDirty(key);
        }
    }

    private class CacheEntry
    {
        public required IRenderable Content { get; set; }
        public DateTime LastAccessed { get; set; }
    }
}
