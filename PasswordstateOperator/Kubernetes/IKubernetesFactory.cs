using k8s;

namespace PasswordstateOperator.Kubernetes
{
    public interface IKubernetesFactory
    {
        IKubernetes Create();
    }
}