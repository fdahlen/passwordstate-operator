using System;
using System.Net;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using Microsoft.Rest;

namespace PasswordstateOperator.Kubernetes
{
    public class KubernetesSdk : IKubernetesSdk
    {
        private readonly IKubernetes kubernetes;
        
        public KubernetesSdk(IKubernetesFactory kubernetesFactory)
        {
            kubernetes = kubernetesFactory.Create();
        }

        public Watcher<TCrd> WatchCustomResources<TCrd>(
            string group,
            string version,
            string @namespace,
            string plural,
            Action<WatchEventType, TCrd> onChange,
            Action<Exception> onError,
            Action onClose)
        {
            return kubernetes.ListNamespacedCustomObjectWithHttpMessagesAsync(
                    group,
                    version,
                    @namespace,
                    plural,
                    watch: true)
                .Watch(
                    onChange,
                    onError,
                    onClose);
        }

        public async Task<bool> CustomResourcesExistAsync(
            string group,
            string version,
            string @namespace,
            string plural)
        {
            try
            {
                await kubernetes.ListNamespacedCustomObjectWithHttpMessagesAsync(
                    group,
                    version,
                    @namespace,
                    plural);

                return true;
            }
            catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }
        }
        public async Task<V1Secret> CreateSecretAsync(V1Secret secret, string @namespace)
        {
            return await kubernetes.CreateNamespacedSecretAsync(secret, @namespace);
        }
        
        public async Task<V1Secret> GetSecretAsync(string name, string @namespace)
        {
            try
            {
                return await kubernetes.ReadNamespacedSecretAsync(name, @namespace);
            }
            catch (HttpOperationException hoex) when (hoex.Response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }
        
        public async Task<V1Secret> ReplaceSecretAsync(V1Secret newSecret, string name, string @namespace)
        {
            return await kubernetes.ReplaceNamespacedSecretAsync(newSecret, name, @namespace);
        }
        
        public async Task<bool> DeleteSecretAsync(string name, string @namespace)
        {
            try
            {
                await kubernetes.DeleteNamespacedSecretAsync(name, @namespace);
                return true;
            }
            catch (HttpOperationException hoex) when (hoex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return false;
            }
        }
    }
}