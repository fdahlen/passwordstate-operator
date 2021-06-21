namespace PasswordstateOperator
{
    public class Spec
    {
        public string ServerBaseUrl { get; set; }
        public int PasswordListID { get; set; }
        public string ApiKeySecret { get; set; }
        public string PasswordsSecret { get; set; }
        
        public override string ToString()
        {
            return $"{ServerBaseUrl}:{PasswordListID}:{ApiKeySecret}:{PasswordsSecret}"; 
        }
    }
}