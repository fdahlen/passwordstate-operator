using System;
using System.Net;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Rest;

namespace PasswordstateOperator.Kubernetes
{
    public class KubernetesSdk : IKubernetesSdk
    {
        private readonly IKubernetes kubernetes;
        
        private readonly ILogger<KubernetesSdk> logger;

        public KubernetesSdk(IKubernetesFactory kubernetesFactory, ILogger<KubernetesSdk> logger)
        {
            this.logger = logger;
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
            var watcher = kubernetes.ListNamespacedCustomObjectWithHttpMessagesAsync(
                    group,
                    version,
                    @namespace,
                    plural,
                    watch: true)
                .Watch(
                    onChange,
                    onError,
                    onClose);
            
            logger.LogDebug($"{nameof(WatchCustomResources)}: Watcher created for {plural} resources in namespace {@namespace}");
            
            return watcher;
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
                    @group,
                    version,
                    @namespace,
                    plural);

                logger.LogDebug($"{nameof(CustomResourcesExistAsync)}: There is at least one {plural} resource in namespace {@namespace}");
                
                return true;
            }
            catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
            {
                logger.LogDebug($"{nameof(CustomResourcesExistAsync)}: There are no {plural} resources in namespace {@namespace}");
                
                return false;
            }
        }
        
        public async Task CreateSecretAsync(V1Secret secret, string @namespace)
        {
            try
            {
                await kubernetes.CreateNamespacedSecretAsync(secret, @namespace);
                logger.LogDebug($"{nameof(CreateSecretAsync)}: Created secret with name {secret.Name()} in namespace {@namespace}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"{nameof(CreateSecretAsync)}: Failed to create secret with name {secret.Name()} in namespace {@namespace}");
                throw;
            }
        }
        
        public async Task<V1Secret> GetSecretAsync(string name, string @namespace)
        {
            try
            {
                var result = await kubernetes.ReadNamespacedSecretAsync(name, @namespace);
                logger.LogDebug($"{nameof(GetSecretAsync)}: Found secret with name {name} in namespace {@namespace}");
                return result;
            }
            catch (HttpOperationException hoex) when (hoex.Response.StatusCode == HttpStatusCode.NotFound)
            {
                logger.LogDebug($"{nameof(GetSecretAsync)}: Found no secret with name {name} in namespace {@namespace}");
                return null;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"{nameof(GetSecretAsync)}: Failed to get secret with name {name} in namespace {@namespace}");
                throw;
            }
        }
        
        public async Task ReplaceSecretAsync(V1Secret newSecret, string name, string @namespace)
        {
            try
            {
                await kubernetes.ReplaceNamespacedSecretAsync(newSecret, name, @namespace);
                logger.LogDebug($"{nameof(ReplaceSecretAsync)}: Replaced secret with name {name} in namespace {@namespace}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"{nameof(ReplaceSecretAsync)}: Failed to replace secret with name {name} in namespace {@namespace}");
                throw;
            }
        }
        
        public async Task DeleteSecretAsync(string name, string @namespace)
        {
            try
            {
                await kubernetes.DeleteNamespacedSecretAsync(name, @namespace);
                logger.LogDebug($"{nameof(DeleteSecretAsync)}: Deleted secret with name {name} in namespace {@namespace}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"{nameof(DeleteSecretAsync)}: Failed to delete secret with name {name} in namespace {@namespace}");
                throw;
            }
        }
    }
}