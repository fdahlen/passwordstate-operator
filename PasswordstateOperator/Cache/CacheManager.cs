using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PasswordstateOperator.Cache
{
    public class CacheManager
    {
        private readonly ConcurrentDictionary<string, SemaphoreSlim> guards = new();
        private readonly ConcurrentDictionary<string, CacheEntry> cache = new();

        public async Task<CacheLock> GetLock(string id)
        {
            var guard = guards.GetOrAdd(id, new SemaphoreSlim(1, 1));
            await guard.WaitAsync();

            return new CacheLock(id, guard, cache);
        }

        public List<string> GetCachedIds()
        {
            return guards.Keys.ToList();
        }
    }
}