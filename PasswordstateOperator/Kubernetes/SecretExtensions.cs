using System;
using System.Linq;
using System.Text;
using k8s.Models;

namespace PasswordstateOperator.Kubernetes
{
    public static class SecretExtensions
    {
        public static bool DataEquals(this V1Secret first, V1Secret second)
        {
            return GetDataContent(first) == GetDataContent(second);
        }
        
        private static string GetDataContent(V1Secret secret)
        {
            if (secret.Data != null && secret.StringData != null)
            {
                throw new ArgumentException($"Only one of {nameof(secret.Data)} and {nameof(secret.StringData)} can be specified");
            }

            string content = null;
            
            if (secret.StringData != null)
            {
                content = string.Join("|", secret.StringData.Select(kvp => $"{kvp.Key}:{kvp.Value}")
                    .OrderBy(s => s));
            }

            if (secret.Data != null)
            {
                content = string.Join("|", 
                    secret.Data
                        .Select(kvp => $"{kvp.Key}:{Encoding.UTF8.GetString(kvp.Value)}")
                        .OrderBy(s => s));
            }

            return string.IsNullOrEmpty(content) 
                ? null
                : content;
        }
    }
}