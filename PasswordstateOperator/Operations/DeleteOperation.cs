using System.Threading.Tasks;
using k8s.Models;
using PasswordstateOperator.Kubernetes;

namespace PasswordstateOperator.Operations
{
    public class DeleteOperation : IDeleteOperation
    {
        private readonly IKubernetesSdk kubernetesSdk;
        
        public DeleteOperation(IKubernetesSdk kubernetesSdk)
        {
            this.kubernetesSdk = kubernetesSdk;
        }
        
        public async Task Delete(PasswordListCrd crd)
        {
            await kubernetesSdk.DeleteSecretAsync(crd.Spec.SecretName, crd.Namespace());
        }
    }
}