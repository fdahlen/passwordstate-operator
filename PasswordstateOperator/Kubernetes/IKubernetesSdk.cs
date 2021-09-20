using System;
using System.Threading.Tasks;
using k8s;
using k8s.Models;

namespace PasswordstateOperator.Kubernetes
{
    public interface IKubernetesSdk
    {
        Watcher<TCrd> WatchCustomResources<TCrd>(
            string group,
            string version,
            string plural,
            Action<WatchEventType, TCrd> onChange,
            Action<Exception> onError,
            Action onClose);

        Task CreateSecretAsync(V1Secret secret, string @namespace);

        Task<V1Secret> GetSecretAsync(string name, string @namespace);

        Task ReplaceSecretAsync(V1Secret newSecret, string name, string @namespace);

        Task DeleteSecretAsync(string name, string @namespace);

        Task RestartDeploymentAsync(string name, string @namespace);
    }
}