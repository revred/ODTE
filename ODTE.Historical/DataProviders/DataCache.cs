using System;
using System.Collections.Concurrent;

namespace ODTE.Historical.DataProviders;

/// <summary>
/// Thread-safe memory cache with expiration
/// </summary>
public class DataCache : IDisposable
{
    private readonly ConcurrentDictionary<string, CacheItem> _cache;
    private readonly TimeSpan _defaultExpiration;
    private readonly object _cleanupLock = new();
    private DateTime _lastCleanup = DateTime.UtcNow;
    
    public DataCache(TimeSpan defaultExpiration)
    {
        _cache = new ConcurrentDictionary<string, CacheItem>();
        _defaultExpiration = defaultExpiration;
    }
    
    public void Set<T>(string key, T value, TimeSpan? expiration = null)
    {
        var item = new CacheItem
        {
            Value = value,
            ExpiresAt = DateTime.UtcNow + (expiration ?? _defaultExpiration)
        };
        
        _cache.AddOrUpdate(key, item, (k, v) => item);
        
        // Cleanup old items periodically
        CleanupIfNeeded();
    }
    
    public T? Get<T>(string key) where T : class
    {
        if (!_cache.TryGetValue(key, out var item))
            return null;
        
        if (item.ExpiresAt < DateTime.UtcNow)
        {
            _cache.TryRemove(key, out _);
            return null;
        }
        
        return item.Value as T;
    }
    
    public bool Remove(string key)
    {
        return _cache.TryRemove(key, out _);
    }
    
    public void Clear()
    {
        _cache.Clear();
    }
    
    public int Count => _cache.Count;
    
    private void CleanupIfNeeded()
    {
        // Only cleanup once per minute
        if (DateTime.UtcNow - _lastCleanup < TimeSpan.FromMinutes(1))
            return;
        
        lock (_cleanupLock)
        {
            if (DateTime.UtcNow - _lastCleanup < TimeSpan.FromMinutes(1))
                return;
            
            var now = DateTime.UtcNow;
            var keysToRemove = new List<string>();
            
            foreach (var kvp in _cache)
            {
                if (kvp.Value.ExpiresAt < now)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }
            
            foreach (var key in keysToRemove)
            {
                _cache.TryRemove(key, out _);
            }
            
            _lastCleanup = DateTime.UtcNow;
        }
    }
    
    public void Dispose()
    {
        Clear();
    }
    
    private class CacheItem
    {
        public object? Value { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}