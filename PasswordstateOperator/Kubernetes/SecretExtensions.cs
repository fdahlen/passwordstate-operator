using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.Json;
using k8s.Models;

namespace PasswordstateOperator.Kubernetes
{
    public static class SecretExtensions
    {
        public static bool DataEquals(this V1Secret first, V1Secret second)
        {
            return GetContent(first).Equals(GetContent(second));
        }
        
        private static string GetContent(V1Secret secret)
        {
            if (secret.StringData != null)
            {
                return string.Join("|", secret.StringData.Select(kvp => $"{kvp.Key}:{kvp.Value}").OrderBy(s => s));
            }

            if (secret.Data != null)
            {
                return string.Join("|", secret.Data.Select(kvp => $"{kvp.Key}:{Encoding.UTF8.GetString(kvp.Value)}").OrderBy(s => s));
            }

            throw new ArgumentException("No data in secret");
        }
    }
}