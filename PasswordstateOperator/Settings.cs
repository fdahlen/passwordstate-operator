using System;
using System.IO;
using System.Threading.Tasks;

namespace PasswordstateOperator
{
    public class Settings
    {
        private const int DEFAULT_SYNC_INTERVAL_SECONDS = 60;

        public string ServerBaseUrl { get; set; } = Environment.GetEnvironmentVariable("SERVER_BASE_URL");
        public string ApiKeyPath { get; set; } = Environment.GetEnvironmentVariable("API_KEY_PATH");
        public int SyncIntervalSeconds { get; set; } = int.Parse(Environment.GetEnvironmentVariable("SYNC_INTERVAL_SECONDS") ?? DEFAULT_SYNC_INTERVAL_SECONDS.ToString());

        public string ApiKey { private get; set; }

        public async Task<string> GetApiKey()
        {
            if (string.IsNullOrEmpty(ApiKey))
            {
                ApiKey = await File.ReadAllTextAsync(ApiKeyPath);
            }

            if (string.IsNullOrEmpty(ApiKey))
            {
                throw new ApplicationException($"{nameof(GetApiKey)}: api key file was empty '{ApiKeyPath}'");
            }

            return ApiKey;
        }
    }
}
