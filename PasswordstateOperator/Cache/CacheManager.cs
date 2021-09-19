using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace PasswordstateOperator.Cache
{
    public class CacheManager
    {
        private readonly ConcurrentDictionary<string, PasswordListCrd> cache = new();

        public void AddOrUpdate(string id, PasswordListCrd crd)
        {
            cache.AddOrUpdate(id, crd, (_, _) => crd);
        }

        public PasswordListCrd Get(string id)
        {
            return cache.TryGetValue(id, out var crd) ? crd : null;
        }

        public IList<PasswordListCrd> List()
        {
            return cache.Values.ToList();
        }

        public void Delete(string id)
        {
            if (!cache.TryRemove(id, out _))
            {
                throw new ApplicationException($"Failed to remove id '{id}' from cache");
            }
        }
    }
}