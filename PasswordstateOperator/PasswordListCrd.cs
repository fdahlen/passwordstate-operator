using k8s;
using k8s.Models;

namespace PasswordstateOperator
{
    public class PasswordListCrd: IMetadata<V1ObjectMeta>
    {
        public const string ApiGroup = "passwordstate.operator";

        public const string ApiVersion = "v1";

        public const string Singular = "passwordlist";
        
        public const string Plural = "passwordlists";

        public const string Kind = "PasswordList";

        public string ID => $"{this.Namespace()}/{this.Name()}";

        public V1ObjectMeta Metadata { get; set; }

        public string StatusAnnotationName => string.Format(ApiGroup + "/" + Singular + "-status");

        public string Status => !Metadata.Annotations.ContainsKey(StatusAnnotationName) ? null : Metadata.Annotations[StatusAnnotationName];

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
