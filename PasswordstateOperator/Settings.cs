using System;

namespace PasswordstateOperator
{
    public class Settings
    {
        public string ServerBaseUrl => Environment.GetEnvironmentVariable("SERVER_BASE_URL");
        public string ApiKeyPath => Environment.GetEnvironmentVariable("API_KEY_PATH");
        public int SyncIntervalSeconds => int.Parse(Environment.GetEnvironmentVariable("SYNC_INTERVAL_SECONDS") ?? "60");
    }
}