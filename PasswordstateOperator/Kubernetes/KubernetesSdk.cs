using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using Microsoft.AspNetCore.JsonPatch;
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
            string plural,
            Action<WatchEventType, TCrd> onChange,
            Action<Exception> onError,
            Action onClose)
        {
            const string allNamespaces = "";

            var watcher = kubernetes.ListNamespacedCustomObjectWithHttpMessagesAsync(
                    group,
                    version,
                    allNamespaces,
                    plural,
                    watch: true)
                .Watch(
                    onChange,
                    onError,
                    onClose);

            logger.LogDebug($"{nameof(WatchCustomResources)}: Watcher created for {plural}");

            return watcher;
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

        public async Task RestartDeployment(string name, string @namespace)
        {
            var deployment = await kubernetes.ReadNamespacedDeploymentAsync(name, @namespace);
            var annotationsWithRestart = new Dictionary<string, string>(deployment.Metadata.Annotations)
            {
                ["kubectl.kubernetes.io/restartedAt"] = DateTime.UtcNow.ToString("s")
            };

            var jsonPatch = new JsonPatchDocument<V1Deployment>();
            jsonPatch.Replace(e => e.Spec.Template.Metadata.Annotations, annotationsWithRestart);
            var patch = new V1Patch(jsonPatch, V1Patch.PatchType.JsonPatch);

            await kubernetes.PatchNamespacedDeploymentAsync(patch, name, @namespace);
        }
    }
}