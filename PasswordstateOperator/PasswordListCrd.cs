using k8s;
using k8s.Models;

namespace PasswordstateOperator
{
    public class PasswordListCrd : IMetadata<V1ObjectMeta>
    {
        public const string ApiGroup = "passwordstate.operator";

        public const string ApiVersion = "v1";

        public const string Plural = "passwordlists";

        public string Id => $"{this.Namespace()}/{this.Name()}";

        public V1ObjectMeta Metadata { get; set; }

        public Spec Spec { get; set; }

        public override bool Equals(object obj)
        {
            return obj != null && ToString().Equals(obj.ToString());
        }

        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }

        public override string ToString()
        {
            return Spec.ToString();
        }
    }
}