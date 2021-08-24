using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using k8s.Models;
using Microsoft.Extensions.Logging;
using PasswordstateOperator.Cache;
using PasswordstateOperator.Kubernetes;
using PasswordstateOperator.Passwordstate;

namespace PasswordstateOperator
{
    public class OperationHandler
    {
        private readonly ILogger<OperationHandler> logger;
        private readonly CacheManager cacheManager = new();
        private readonly PasswordstateSdk passwordstateSdk;
        private readonly IKubernetesSdk kubernetesSdk;

        public OperationHandler(ILogger<OperationHandler> logger, PasswordstateSdk passwordstateSdk, IKubernetesSdk kubernetesSdk)
        {
            this.logger = logger;
            this.passwordstateSdk = passwordstateSdk;
            this.kubernetesSdk = kubernetesSdk;
        }

        public async Task OnAdded(PasswordListCrd crd)
        {
            logger.LogInformation($"{nameof(OnAdded)}: {crd.Id}");

            using var cacheLock = await cacheManager.GetLock(crd.Id);
            await CreatePasswordsSecret(crd, cacheLock);
        }

        public Task OnBookmarked(PasswordListCrd crd)
        {
             logger.LogInformation($"{nameof(OnBookmarked)}: {crd.Id}");

            return Task.CompletedTask;
        }

        public async Task OnDeleted(PasswordListCrd crd)
        {
            logger.LogInformation($"{nameof(OnDeleted)}: {crd.Id}");

            using var cacheLock = await cacheManager.GetLock(crd.Id);
            await DeletePasswordsSecret(crd, cacheLock);
        }

        private async Task DeletePasswordsSecret(PasswordListCrd crd, CacheLock cacheLock)
        {
            await kubernetesSdk.DeleteSecretAsync(crd.Spec.PasswordsSecret, crd.Namespace());

            if (!cacheLock.TryRemoveFromCache())
            {
                throw new ApplicationException($"Failed to remove from cache: {crd.Id}");
            }
        }

        public Task OnError(PasswordListCrd crd)
        {
            logger.LogError($"{nameof(OnError)}: {crd.Id}");

            return Task.CompletedTask;
        }

        public async Task OnUpdated(PasswordListCrd newCrd)
        {
            logger.LogInformation($"{nameof(OnUpdated)}: {newCrd.Id}");

            using var cacheLock = await cacheManager.GetLock(newCrd.Id);
            await UpdatePasswordsSecretIfRequired(newCrd, cacheLock);
        }

        public async Task CheckCurrentState()
        {
            logger.LogDebug(nameof(CheckCurrentState));

            foreach (var id in cacheManager.GetCachedIds())
            {
                using var cacheLock = await cacheManager.GetLock(id);
                await CheckCurrentStateForCrd(id, cacheLock);
            }
        }
        
        private async Task CheckCurrentStateForCrd(string id, CacheLock cacheLock)
        {
            if (!cacheLock.TryGetFromCache(out var cacheEntry))
            {
                logger.LogWarning($"{nameof(CheckCurrentStateForCrd)}: {id}: expected existing crd but none found in cache, will skip");
                return;
            }

            var crd = cacheEntry.Crd;
            
            var passwordsSecret = await kubernetesSdk.GetSecretAsync(crd.Spec.PasswordsSecret, crd.Namespace());
            if (passwordsSecret == null)
            {
                logger.LogInformation($"{nameof(CheckCurrentStateForCrd)}: {crd.Id}: does not exist, will create");

                await CreatePasswordsSecret(crd, cacheLock);
            }
            else
            {
                logger.LogDebug($"{nameof(CheckCurrentStateForCrd)}: {crd.Id}: exists");

                var shouldSync = crd.Spec.SyncIntervalSeconds > 0 && DateTimeOffset.UtcNow > cacheEntry.SyncTime.AddSeconds(crd.Spec.SyncIntervalSeconds);
                if (shouldSync)
                {
                    logger.LogDebug($"{nameof(CheckCurrentStateForCrd)}: {crd.Id}: {crd.Spec.SyncIntervalSeconds}s has passed, will sync with Passwordstate");

                    var passwordsJson = await SyncWithPasswordstate(cacheEntry.Crd, cacheEntry.PasswordsJson);

                    var newCacheEntry = new CacheEntry(cacheEntry.Crd, passwordsJson, DateTimeOffset.UtcNow);
                    cacheLock.AddOrUpdateInCache(newCacheEntry);
                }
            }
        }

        private async Task<string> SyncWithPasswordstate(PasswordListCrd crd, string currentPasswordsJson)
        {
            var newPasswords = await FetchPasswordListFromPasswordstate(crd);

            if (newPasswords.Json == currentPasswordsJson)
            {
                logger.LogDebug($"{nameof(SyncWithPasswordstate)}: {crd.Id}: no changes in Passwordstate, will skip");
                return currentPasswordsJson;
            }

            var newPasswordsSecret = BuildSecret(crd, newPasswords.Passwords);

            logger.LogInformation($"{nameof(SyncWithPasswordstate)}: {crd.Id}: detected changed password list in Passwordstate, will update password secret '{crd.Spec.PasswordsSecret}'");

            await kubernetesSdk.ReplaceSecretAsync(newPasswordsSecret, crd.Spec.PasswordsSecret, crd.Namespace());

            return newPasswords.Json;
        }

        private async Task CreatePasswordsSecret(PasswordListCrd crd, CacheLock cacheLock)
        {

            // Make sure we have CRD in cache, so reconciliation can fix things
            cacheLock.AddOrUpdateInCache(new CacheEntry(crd, null, DateTimeOffset.MinValue));

            PasswordListResponse passwords;
            try
            {
                passwords = await FetchPasswordListFromPasswordstate(crd);
            }
            catch (Exception e)
            {
                logger.LogError(e, $"{nameof(CreatePasswordsSecret)}: {crd.Id}: Got exception, will not create secret '{crd.Spec.PasswordsSecret}'");
                return;
            }

            var passwordsSecret = BuildSecret(crd, passwords.Passwords);
            
            //TODO: crashed because of Http status code Conflict when already in k8s
            await kubernetesSdk.CreateSecretAsync(passwordsSecret, crd.Namespace());

            cacheLock.AddOrUpdateInCache(new CacheEntry(crd, passwords.Json, DateTimeOffset.UtcNow));

            logger.LogInformation($"{nameof(CreatePasswordsSecret)}: {crd.Id}: successfully created '{crd.Spec.PasswordsSecret}'");
        }
        
        private async Task<PasswordListResponse> FetchPasswordListFromPasswordstate(PasswordListCrd crd)
        {
            var apiKey = await GetApiKey(crd);

            return await passwordstateSdk.GetPasswordList(crd.Spec.ServerBaseUrl, crd.Spec.PasswordListId, apiKey);
        }
        
        private async Task<string> GetApiKey(PasswordListCrd crd)
        {
            var apiKeySecret = await kubernetesSdk.GetSecretAsync(crd.Spec.ApiKeySecret, crd.Namespace());
            if (apiKeySecret == null)
            {
                throw new ApplicationException($"{nameof(GetApiKey)}: {crd.Id}: api key secret '{crd.Spec.ApiKeySecret}' was not found");
            }

            const string dataName = "apikey";
            if (!apiKeySecret.Data.TryGetValue(dataName, out var apiKeyBytes))
            {
                throw new ApplicationException($"{nameof(GetApiKey)}: {crd.Id}: data field '{dataName}' was not found in api key secret '{crd.Spec.ApiKeySecret}'");
            }

            return Encoding.UTF8.GetString(apiKeyBytes);
        }

        private V1Secret BuildSecret(PasswordListCrd crd, List<Password> passwords)
        {
            var flattenedPasswords = new Dictionary<string, string>();

            foreach (var password in passwords)
            {
                const string TitleField = "Title";
                var title = password.Fields.FirstOrDefault(field => field.Name == TitleField);

                if (title == null)
                {
                    var passwordId = password.Fields.FirstOrDefault(field => field.Name == "PasswordID");
                    logger.LogWarning($"{nameof(BuildSecret)}: {crd.Id}: No {TitleField} found, skipping password ID {passwordId} in list ID {crd.Spec.PasswordListId}");
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
                    
                    var key = Clean($"{title.Value}.{field.Name}");                    
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
            return Regex.Replace(secretKey, "[^A-Za-z0-9_.-]", "").ToLower();
        }

        private async Task UpdatePasswordsSecretIfRequired(PasswordListCrd newCrd, CacheLock cacheLock)
        {
            if (!cacheLock.TryGetFromCache(out var cacheEntry))
            {
                logger.LogWarning($"{nameof(UpdatePasswordsSecretIfRequired)}: {newCrd.Id}: expected existing crd but none found, will create new");
                await CreatePasswordsSecret(newCrd, cacheLock);
                return;
            }
            
            var currentCrd = cacheEntry.Crd;
            
            if (currentCrd.Spec.ToString() == newCrd.Spec.ToString())
            {
                logger.LogDebug($"{nameof(UpdatePasswordsSecretIfRequired)}: {newCrd.Id}: identical Spec, will not update");
                return;
            }

            logger.LogInformation($"{nameof(UpdatePasswordsSecretIfRequired)}: {newCrd.Id}: detected updated crd, will delete existing and create new");

            await DeletePasswordsSecret(currentCrd, cacheLock);
            await CreatePasswordsSecret(newCrd, cacheLock);
        }
    }
}