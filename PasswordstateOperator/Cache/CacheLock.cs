using System;
using System.Collections.Concurrent;
using System.Threading;

namespace PasswordstateOperator.Cache
{
    public sealed class CacheLock : IDisposable
    {
        private readonly string id;
        private readonly SemaphoreSlim guard;
        private readonly ConcurrentDictionary<string, CacheEntry> cache;

        public CacheLock(string id, SemaphoreSlim guard, ConcurrentDictionary<string, CacheEntry> cache)
        {
            this.id = id;
            this.guard = guard;
            this.cache = cache;
        }
        
        public void Dispose()
        {
            guard.Release();
        }
        
        public void AddOrUpdateInCache(CacheEntry cacheEntry)
        {
            cache[id] = cacheEntry;
        }
        
        public bool TryRemoveFromCache()
        {
            return cache.TryRemove(id, out _);
        }
        
        public bool TryGetFromCache(out CacheEntry cacheEntry)
        {
            return cache.TryGetValue(id, out cacheEntry);
        }
    }
}