using PasswordstateOperator.Passwordstate;

namespace PasswordstateOperator.Cache
{
    public class CacheEntry
    {
        public PasswordListCrd Crd { get; }

        public string PasswordsJson { get; }

        public CacheEntry(PasswordListCrd crd, string passwordsJson)
        {
            Crd = crd;
            PasswordsJson = passwordsJson;
        }
    }
}