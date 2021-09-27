using System.Threading.Tasks;
using k8s.Models;
using Microsoft.Extensions.Logging;
using PasswordstateOperator.Kubernetes;

namespace PasswordstateOperator.Operations
{
    public class UpdateOperation : IUpdateOperation
    {
        private readonly ILogger<UpdateOperation> logger;
        private readonly IKubernetesSdk kubernetesSdk;
        private readonly IDeleteOperation deleteOperation;
        private readonly ICreateOperation createOperation;
        
        public UpdateOperation(ILogger<UpdateOperation> logger, IKubernetesSdk kubernetesSdk, IDeleteOperation deleteOperation, ICreateOperation createOperation)
        {
            this.logger = logger;
            this.kubernetesSdk = kubernetesSdk;
            this.deleteOperation = deleteOperation;
            this.createOperation = createOperation;
        }

        public async Task Update(PasswordListCrd existingCrd, PasswordListCrd newCrd)
        {
            if (existingCrd == null)
            {
                logger.LogWarning($"{nameof(Update)}: {newCrd.Id}: expected existing crd in cache but none found, will try to create new password secret");
                await createOperation.Create(newCrd);
                return;
            }

            if (existingCrd.Spec.Equals(newCrd.Spec))
            {
                logger.LogDebug($"{nameof(Update)}: {newCrd.Id}: identical Spec, will not update password secret");
                return;
            }

            logger.LogInformation($"{nameof(Update)}: {newCrd.Id}: detected updated crd, will delete existing password secret and create new");

            await deleteOperation.Delete(existingCrd);
            await createOperation.Create(newCrd);

            if (newCrd.Spec.AutoRestartDeploymentName != null)
            {
                logger.LogInformation($"{nameof(Update)}: {newCrd.Id}: will restart deployment '{newCrd.Spec.AutoRestartDeploymentName}'");
                await kubernetesSdk.RestartDeploymentAsync(newCrd.Spec.AutoRestartDeploymentName, newCrd.Namespace());
            }
        }
    }
}