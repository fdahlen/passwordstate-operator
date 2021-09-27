using System.Threading.Tasks;
using k8s.Models;
using PasswordstateOperator.Kubernetes;

namespace PasswordstateOperator.Operations
{
    public class GetOperation : IGetOperation
    {
        private readonly IKubernetesSdk kubernetesSdk;
        
        public GetOperation(IKubernetesSdk kubernetesSdk)
        {
            this.kubernetesSdk = kubernetesSdk;
        }
        
        public async Task<V1Secret> Get(PasswordListCrd crd)
        {
            return await kubernetesSdk.GetSecretAsync(crd.Spec.SecretName, crd.Namespace());
        }
    }
}