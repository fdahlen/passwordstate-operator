using System;

namespace PasswordstateOperator
{
    public class Spec
    {
        public string ServerBaseUrl { get; set; }
        public string PasswordListId { get; set; }
        public string ApiKeySecret { get; set; }
        public string PasswordsSecret { get; set; }
        public int SyncIntervalSeconds { get; set; } = 60;

        public override string ToString()
        {
            return $"{ServerBaseUrl}:{PasswordListId}:{ApiKeySecret}:{PasswordsSecret}:{SyncIntervalSeconds}"; 
        }
        
        public override bool Equals(object obj)
        {
            return ToString().Equals(obj?.ToString());
        }
        
        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }
    }
}