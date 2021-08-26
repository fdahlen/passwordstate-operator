using System.Text.Json;
using k8s.Models;

namespace PasswordstateOperator.Kubernetes
{
    public static class SecretExtensions
    {
        public static bool DataEquals(this V1Secret first, V1Secret second)
        {
            var firstJson = JsonSerializer.Serialize(first.Data);
            var secondJson = JsonSerializer.Serialize(second.Data);

            return firstJson.Equals(secondJson);
        }
    }
}