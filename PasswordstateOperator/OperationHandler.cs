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
        private const int SyncIntervalSeconds = 60;

        private readonly DateTimeOffset previousSyncTime = DateTimeOffset.MinValue;
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

            cacheManager.AddOrUpdate(crd.Id, crd);
            
            await CreatePasswordsSecret(crd);
        }

        public async Task OnUpdated(PasswordListCrd newCrd)
        {
            logger.LogInformation($"{nameof(OnUpdated)}: {newCrd.Id}");

            var existingCrd = cacheManager.Get(newCrd.Id);
            cacheManager.AddOrUpdate(newCrd.Id, newCrd);
            
            await UpdatePasswordsSecret(existingCrd, newCrd);
        }

        public async Task OnDeleted(PasswordListCrd crd)
        {
            logger.LogInformation($"{nameof(OnDeleted)}: {crd.Id}");

            cacheManager.Delete(crd.Id);
            
            await DeletePasswordsSecret(crd);
        }

        public Task OnBookmarked(PasswordListCrd crd)
        {
            logger.LogInformation($"{nameof(OnBookmarked)}: {crd.Id}");

            return Task.CompletedTask;
        }

        public Task OnError(PasswordListCrd crd)
        {
            logger.LogError($"{nameof(OnError)}: {crd.Id}");

            return Task.CompletedTask;
        }

        public async Task CheckCurrentState()
        {
            logger.LogDebug(nameof(CheckCurrentState));
            
            var sync = DateTimeOffset.UtcNow >= previousSyncTime.AddSeconds(SyncIntervalSeconds);
            if (sync)
            {
                logger.LogDebug($"{nameof(CheckCurrentState)}: {SyncIntervalSeconds}s has passed, will sync with Passwordstate");
            }

            foreach (var crd in cacheManager.List())
            {
                await CheckCurrentStateForCrd(crd, sync);
            }
        }
        
        private async Task CheckCurrentStateForCrd(PasswordListCrd crd, bool sync)
        {
            var passwordsSecret = await kubernetesSdk.GetSecretAsync(crd.Spec.PasswordsSecret, crd.Namespace());
            if (passwordsSecret == null)
            {
                logger.LogInformation($"{nameof(CheckCurrentStateForCrd)}: {crd.Id}: password secret does not exist, will create");

                await CreatePasswordsSecret(crd);
            }
            else
            {
                logger.LogDebug($"{nameof(CheckCurrentStateForCrd)}: {crd.Id}: password secret exists");

                if (sync)
                {
                    logger.LogDebug($"{nameof(CheckCurrentStateForCrd)}: {crd.Id}: will sync");

                    await SyncExistingPasswordSecretWithPasswordstate(crd, passwordsSecret);
                }
            }
        }
        
        private async Task SyncExistingPasswordSecretWithPasswordstate(PasswordListCrd crd, V1Secret existingPasswordsSecret)
        {
            PasswordListResponse newPasswords;
            try
            {
                newPasswords = await FetchPasswordListFromPasswordstate(crd);
            }
            catch (Exception e)
            {
                logger.LogError(e, $"{nameof(SyncExistingPasswordSecretWithPasswordstate)}: {crd.Id}: Got exception, will not sync password secret '{crd.Spec.PasswordsSecret}'");
                return;
            }
            
            var newPasswordsSecret = BuildSecret(crd, newPasswords.Passwords);

            if (existingPasswordsSecret.DataEquals(newPasswordsSecret))
            {
                logger.LogDebug($"{nameof(SyncExistingPasswordSecretWithPasswordstate)}: {crd.Id}: no changes in Passwordstate, will skip password secret '{crd.Spec.PasswordsSecret}'");
            }
            else
            {
                logger.LogInformation($"{nameof(SyncExistingPasswordSecretWithPasswordstate)}: {crd.Id}: detected changed password list in Passwordstate, will update password secret '{crd.Spec.PasswordsSecret}'");
                await kubernetesSdk.ReplaceSecretAsync(newPasswordsSecret, crd.Spec.PasswordsSecret, crd.Namespace());
            }
        }

        private async Task CreatePasswordsSecret(PasswordListCrd crd)
        {
            var existingPasswordsSecret = await kubernetesSdk.GetSecretAsync(crd.Spec.PasswordsSecret, crd.Namespace());
            if (existingPasswordsSecret == null)
            {
                PasswordListResponse passwords;
                try
                {
                    passwords = await FetchPasswordListFromPasswordstate(crd);
                }
                catch (Exception e)
                {
                    logger.LogError(e, $"{nameof(CreatePasswordsSecret)}: {crd.Id}: Got exception, will not create password secret '{crd.Spec.PasswordsSecret}'");
                    return;
                }
            
                var passwordsSecret = BuildSecret(crd, passwords.Passwords);

                logger.LogInformation($"{nameof(CreatePasswordsSecret)}: {crd.Id}: will create password secret '{crd.Spec.PasswordsSecret}'");
                
                await kubernetesSdk.CreateSecretAsync(passwordsSecret, crd.Namespace());
            }
            else
            {
                await SyncExistingPasswordSecretWithPasswordstate(crd, existingPasswordsSecret);
            }
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

        private async Task UpdatePasswordsSecret(PasswordListCrd existingCrd, PasswordListCrd newCrd)
        {
            if (existingCrd == null)
            {
                logger.LogWarning($"{nameof(UpdatePasswordsSecret)}: {newCrd.Id}: expected existing crd in cache but none found, will try to create new password secret");
                await CreatePasswordsSecret(newCrd);
                return;
            }
            
            if (existingCrd.Spec.Equals(newCrd.Spec))
            {
                logger.LogDebug($"{nameof(UpdatePasswordsSecret)}: {newCrd.Id}: identical Spec, will not update password secret");
                return;
            }

            logger.LogInformation($"{nameof(UpdatePasswordsSecret)}: {newCrd.Id}: detected updated crd, will delete existing password secret and create new");

            await DeletePasswordsSecret(existingCrd);
            await CreatePasswordsSecret(newCrd);
        }

        private async Task DeletePasswordsSecret(PasswordListCrd crd)
        {
            await kubernetesSdk.DeleteSecretAsync(crd.Spec.PasswordsSecret, crd.Namespace());
        }
    }
}