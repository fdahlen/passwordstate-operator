using System;

namespace PasswordstateOperator
{
    public class Settings
    {
        public string ServerBaseUrl => Environment.GetEnvironmentVariable("SERVER_BASE_URL");
        public string ApiKeySecretName => Environment.GetEnvironmentVariable("API_KEY_SECRET_NAME");
        public string ApiKeySecretNamespace => Environment.GetEnvironmentVariable("API_KEY_SECRET_NAMESPACE");
    }
}