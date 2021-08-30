using System;

namespace PasswordstateOperator
{
    public class Spec
    {
        public string ServerBaseUrl { get; set; }
        public string PasswordListId { get; set; }
        public string ApiKeySecret { get; set; }
        public string PasswordsSecret { get; set; }

        public override string ToString()
        {
            return $"{ServerBaseUrl}:{PasswordListId}:{ApiKeySecret}:{PasswordsSecret}"; 
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