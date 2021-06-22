using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PasswordstateOperator
{
    public class State
    {
        private readonly ConcurrentDictionary<string, SemaphoreSlim> guards = new();
        private readonly ConcurrentDictionary<string, Entry> currentState = new();

        public async Task GuardedRun(string id, Action action)
        {
            var guard = guards.GetOrAdd(id, new SemaphoreSlim(1));
            await guard.WaitAsync();

            try
            {
                action();
            }
            finally
            {
                guard.Release();
            }
        }
        
        public class Entry
        {
            public PasswordListCrd Crd { get; }

            public int PasswordsHashCode { get; }

            public Entry(PasswordListCrd crd, int passwordsHashCode)
            {
                Crd = crd;
                PasswordsHashCode = passwordsHashCode;
            }
        }

        public bool TryRemove(string id)
        {
            return currentState.TryRemove(id, out _);
        }
        
        public List<string> GetIds()
        {
            return guards.Keys.ToList();
        }
        
        public bool TryGet(string id, out Entry entry)
        {
            return currentState.TryGetValue(id, out entry);
        }
        
        public void Add(string id, Entry entry)
        {
            currentState[id] = entry;
        }
    }
}