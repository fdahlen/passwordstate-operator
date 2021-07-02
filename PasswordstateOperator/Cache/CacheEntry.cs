namespace PasswordstateOperator.Cache
{
    public class CacheEntry
    {
        public PasswordListCrd Crd { get; }

        public int PasswordsHashCode { get; }

        public CacheEntry(PasswordListCrd crd, int passwordsHashCode)
        {
            Crd = crd;
            PasswordsHashCode = passwordsHashCode;
        }
    }
}