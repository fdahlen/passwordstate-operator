using System;

namespace PasswordstateOperator.Cache
{
    public class CacheEntry
    {
        public PasswordListCrd Crd { get; }

        public DateTimeOffset PreviousSyncTime { get; }

        public CacheEntry(PasswordListCrd crd, DateTimeOffset previousSyncTime)
        {
            Crd = crd;
            PreviousSyncTime = previousSyncTime;
        }
    }
}