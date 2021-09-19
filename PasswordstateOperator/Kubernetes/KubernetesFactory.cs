using System;
using System.Net.Http;
using k8s;

namespace PasswordstateOperator.Kubernetes
{
    public class KubernetesFactory : IKubernetesFactory
    {
        public IKubernetes Create()
        {
            return new k8s.Kubernetes(
                !KubernetesClientConfiguration.IsInCluster() ? KubernetesClientConfiguration.BuildConfigFromConfigFile() : KubernetesClientConfiguration.InClusterConfig(),
                Array.Empty<DelegatingHandler>());
        }
    }
}