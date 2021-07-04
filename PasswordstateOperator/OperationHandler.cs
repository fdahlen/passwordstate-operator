using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Rest;
using PasswordstateOperator.Cache;
using PasswordstateOperator.Passwordstate;

namespace PasswordstateOperator
{
    public class OperationHandler
    {
        private const int PasswordstateSyncIntervalSeconds = 60;
        private DateTimeOffset previousSyncTime = DateTimeOffset.UtcNow;
        
        private readonly ILogger<OperationHandler> logger;
        private readonly CacheManager cacheManager = new();
        private readonly PasswordstateSdk passwordstateSdk;

        public OperationHandler(ILogger<OperationHandler> logger, PasswordstateSdk passwordstateSdk)
        {
            this.logger = logger;
            this.passwordstateSdk = passwordstateSdk;
        }

        public async Task OnAdded(IKubernetes k8s, PasswordListCrd crd)
        {
            logger.LogInformation($"{nameof(OnAdded)}: {crd.Id}");

            using var cacheLock = await cacheManager.GetLock(crd.Id);
            await CreatePasswordsSecret(k8s, crd, cacheLock);
        }

        public Task OnBookmarked(IKubernetes k8s, PasswordListCrd crd)
        {
             logger.LogInformation($"{nameof(OnBookmarked)}: {crd.Id}");

            return Task.CompletedTask;
        }

        public async Task OnDeleted(IKubernetes k8s, PasswordListCrd crd)
        {
            logger.LogInformation($"{nameof(OnDeleted)}: {crd.Id}");

            using var cacheLock = await cacheManager.GetLock(crd.Id);
            await DeletePasswordsSecret(k8s, crd, cacheLock);
        }
        
        private async Task DeletePasswordsSecret(IKubernetes k8s, PasswordListCrd crd, CacheLock cacheLock)
        {
            try
            {
                await k8s.DeleteNamespacedSecretAsync(crd.Spec.PasswordsSecret, crd.Namespace());
                logger.LogInformation($"{nameof(DeletePasswordsSecret)}: {crd.Id}: deleted '{crd.Spec.PasswordsSecret}'");
            }
            catch (HttpOperationException hoex) when (hoex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                logger.LogWarning($"{nameof(DeletePasswordsSecret)}: {crd.Id}: could not delete because not found in k8s '{crd.Spec.PasswordsSecret}'");
            }

            if (!cacheLock.TryRemoveFromCache())
            {
                throw new ApplicationException($"Failed to remove from cache: {crd.Id}");
            }
        }

        public Task OnError(IKubernetes k8s, PasswordListCrd crd)
        {
            logger.LogError($"{nameof(OnError)}: {crd.Id}");

            return Task.CompletedTask;
        }

        public async Task OnUpdated(IKubernetes k8s, PasswordListCrd newCrd)
        {
            logger.LogInformation($"{nameof(OnUpdated)}: {newCrd.Id}");

            using var cacheLock = await cacheManager.GetLock(newCrd.Id);
            await UpdatePasswordsSecretIfRequired(k8s, newCrd, cacheLock);
        }

        public async Task CheckCurrentState(IKubernetes k8s)
        {
            logger.LogInformation(nameof(CheckCurrentState));

            foreach (var id in cacheManager.GetCachedIds())
            {
                using var cacheLock = await cacheManager.GetLock(id);
                await CheckCurrentStateForCrd(k8s, id, cacheLock);
            }
        }
        
        private async Task CheckCurrentStateForCrd(IKubernetes k8s, string id, CacheLock cacheLock)
        {
            if (!cacheLock.TryGetFromCache(out var cacheEntry))
            {
                logger.LogWarning($"{nameof(CheckCurrentStateForCrd)}: {id}: expected existing crd but none found, will skip");
                return;
            }

            var crd = cacheEntry.Crd;
            
            var passwordsSecret = await GetPasswordsSecret(k8s, crd);
            if (passwordsSecret == null)
            {
                logger.LogInformation($"{nameof(CheckCurrentStateForCrd)}: {crd.Id}: does not exist, will create");

                await CreatePasswordsSecret(k8s, crd, cacheLock);
            }
            else
            {
                //TODO: fix bug with sync (only works for 1 crd at the moment)
                
                logger.LogDebug($"{nameof(CheckCurrentStateForCrd)}: {crd.Id}: exists");

                if (DateTimeOffset.UtcNow > previousSyncTime.AddSeconds(PasswordstateSyncIntervalSeconds))
                {
                    previousSyncTime = DateTimeOffset.UtcNow;

                    logger.LogDebug($"{nameof(CheckCurrentStateForCrd)}: {crd.Id}: {PasswordstateSyncIntervalSeconds}s has passed, will sync with Passwordstate");

                    await SyncWithPasswordstate(k8s, cacheEntry);
                }
            }
        }

        private async Task SyncWithPasswordstate(IKubernetes k8s, CacheEntry cacheEntry)
        {
            var crd = cacheEntry.Crd;

            var newPasswords = await FetchPasswordListFromPasswordstate(k8s, crd);

            if (newPasswords.Json != cacheEntry.PasswordsJson)
            {
                var newPasswordsSecret = CreateSecret(crd, newPasswords.Passwords);

                logger.LogInformation($"{nameof(SyncWithPasswordstate)}: {crd.Id}: detected changed password list in Passwordstate, will update password secret '{crd.Spec.PasswordsSecret}'");

                await k8s.ReplaceNamespacedSecretAsync(newPasswordsSecret, crd.Spec.PasswordsSecret, crd.Namespace());
            }
        }

        private static async Task<V1Secret> GetPasswordsSecret(IKubernetes k8s, PasswordListCrd crd)
        {
            try
            {
                return await k8s.ReadNamespacedSecretAsync(crd.Spec.PasswordsSecret, crd.Namespace());
            }
            catch (HttpOperationException hoex) when (hoex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        private async Task CreatePasswordsSecret(IKubernetes k8s, PasswordListCrd crd, CacheLock cacheLock)
        {
            PasswordListResponse passwords;
            
            try
            {
                passwords = await FetchPasswordListFromPasswordstate(k8s, crd);
            }
            catch (Exception e)
            {
                logger.LogError(e, $"{nameof(CreatePasswordsSecret)}: {crd.Id}: Got exception, will not create secret '{crd.Spec.PasswordsSecret}'");
                cacheLock.AddToCache(new CacheEntry(crd, null));
                return;
            }

            var passwordsSecret = CreateSecret(crd, passwords.Passwords);
            await k8s.CreateNamespacedSecretAsync(passwordsSecret, crd.Namespace());

            cacheLock.AddToCache(new CacheEntry(crd, passwords.Json));

            logger.LogInformation($"{nameof(CreatePasswordsSecret)}: {crd.Id}: created '{crd.Spec.PasswordsSecret}'");
        }
        
        private async Task<PasswordListResponse> FetchPasswordListFromPasswordstate(IKubernetes k8s, PasswordListCrd crd)
        {
            V1Secret apiKeySecret;
            try
            {
                apiKeySecret = await k8s.ReadNamespacedSecretAsync(crd.Spec.ApiKeySecret, crd.Namespace());
            }
            catch (HttpOperationException hoex) when (hoex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new ApplicationException($"{nameof(CreatePasswordsSecret)}: {crd.Id}: api key secret was '{crd.Spec.ApiKeySecret}' not found", hoex);
            }

            const string dataName = "apikey";
            if (!apiKeySecret.Data.TryGetValue(dataName, out var apiKeyBytes))
            {
                throw new ApplicationException($"{nameof(CreatePasswordsSecret)}: {crd.Id}: data field '{dataName}' was not found in api key secret '{crd.Spec.ApiKeySecret}' ");
            }

            var apiKey = Encoding.UTF8.GetString(apiKeyBytes);

            return await passwordstateSdk.GetPasswordList(crd.Spec.ServerBaseUrl, crd.Spec.PasswordListId, apiKey);
        }

        private V1Secret CreateSecret(PasswordListCrd crd, List<Password> passwords)
        {
            var flattenedPasswords = new Dictionary<string, string>();

            foreach (var password in passwords)
            {
                const string TitleField = "Title";
                var title = password.Fields.FirstOrDefault(field => field.Name == TitleField);

                if (title == null)
                {
                    var passwordId = password.Fields.FirstOrDefault(field => field.Name == "PasswordID");
                    logger.LogWarning($"{nameof(CreateSecret)}: {crd.Id}: No {TitleField} found, skipping password ID {passwordId} in list ID {crd.Spec.PasswordListId}");
                    continue;
                }

                foreach (var field in password.Fields)
                {
                    if (field.Name == TitleField)
                    {
                        continue;
                    }

                    if (string.IsNullOrEmpty(field.Value))
                    {
                        continue;
                    }
                    
                    var key = Clean($"{title.Name}.{field.Value}");                    
                    flattenedPasswords[key] = field.Value;
                }
            }

            return new V1Secret
            {
                ApiVersion = "v1",
                Kind = "Secret",
                Metadata = new V1ObjectMeta(name: crd.Spec.PasswordsSecret),
                StringData = flattenedPasswords
            };
        }
        
        private static string Clean(string secretKey)
        {
            return Regex.Replace(secretKey, "[^A-Za-z0-9_-.]", "").ToLower();
        }

        private async Task UpdatePasswordsSecretIfRequired(IKubernetes k8s, PasswordListCrd newCrd, CacheLock cacheLock)
        {
            if (!cacheLock.TryGetFromCache(out var cacheEntry))
            {
                logger.LogWarning($"{nameof(OnUpdated)}: {newCrd.Id}: expected existing crd but none found, will create new");
                await CreatePasswordsSecret(k8s, newCrd, cacheLock);
                return;
            }
            
            var currentCrd = cacheEntry.Crd;
            
            if (currentCrd.Spec.ToString() == newCrd.Spec.ToString())
            {
                return;
            }

            logger.LogInformation($"{nameof(OnUpdated)}: {newCrd.Id}: detected updated crd, will delete existing and create new");

            await DeletePasswordsSecret(k8s, currentCrd, cacheLock);
            await CreatePasswordsSecret(k8s, newCrd, cacheLock);
        }
    }
}