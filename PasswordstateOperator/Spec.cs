namespace PasswordstateOperator
{
    public class Spec
    {
        public string PasswordListId { get; set; }
        public string SecretName { get; set; }
        public string AutorestartDeploymentName { get; set; } = null;

        public override string ToString()
        {
            return $"{PasswordListId}:{SecretName}:{AutorestartDeploymentName}"; 
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