using System;

namespace PasswordstateOperator.Cache
{
    public class CacheEntry
    {
        public PasswordListCrd Crd { get; }

        public string PasswordsJson { get; }
        
        public DateTimeOffset SyncTime { get; }

        public CacheEntry(PasswordListCrd crd, string passwordsJson, DateTimeOffset syncTime)
        {
            Crd = crd;
            PasswordsJson = passwordsJson;
            SyncTime = syncTime;
        }
    }
}