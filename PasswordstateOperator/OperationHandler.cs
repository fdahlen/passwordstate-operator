using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using k8s.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
        private readonly Settings settings;
        
        private DateTimeOffset previousSyncTime = DateTimeOffset.MinValue;
        private string apiKey;

        public OperationHandler(
            ILogger<OperationHandler> logger, 
            PasswordstateSdk passwordstateSdk, 
            IKubernetesSdk kubernetesSdk, 
            IOptions<Settings> passwordstateSettings)
        {
            this.logger = logger;
            this.passwordstateSdk = passwordstateSdk;
            this.kubernetesSdk = kubernetesSdk;
            this.settings = passwordstateSettings.Value;
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
            
            await kubernetesSdk.DeleteSecretAsync(crd.Spec.SecretName, crd.Namespace());
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
            
            var sync = DateTimeOffset.UtcNow >= previousSyncTime.AddSeconds(settings.SyncIntervalSeconds);
            if (sync)
            {
                logger.LogDebug($"{nameof(CheckCurrentState)}: {settings.SyncIntervalSeconds}s has passed, will sync with Passwordstate");
            }

            foreach (var crd in cacheManager.List())
            {
                await CheckCurrentStateForCrd(crd, sync);
            }

            if (sync)
            {
                previousSyncTime = DateTimeOffset.UtcNow;
            }
        }
        
        private async Task CheckCurrentStateForCrd(PasswordListCrd crd, bool sync)
        {
            try
            {
                var passwordsSecret = await kubernetesSdk.GetSecretAsync(crd.Spec.SecretName, crd.Namespace());
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
            catch (Exception ex)
            {
                logger.LogError(ex, $"{nameof(CheckCurrentStateForCrd)}: {crd.Id}: Failure");
            }
        }
        
        private async Task SyncExistingPasswordSecretWithPasswordstate(PasswordListCrd crd, V1Secret existingPasswordsSecret)
        {
            var newPasswords = await FetchPasswordListFromPasswordstate(crd);
            var newPasswordsSecret = BuildSecret(crd, newPasswords.Passwords);

            if (existingPasswordsSecret.DataEquals(newPasswordsSecret))
            {
                logger.LogDebug($"{nameof(SyncExistingPasswordSecretWithPasswordstate)}: {crd.Id}: no changes in Passwordstate, will skip password secret '{crd.Spec.SecretName}'");
            }
            else
            {
                logger.LogInformation($"{nameof(SyncExistingPasswordSecretWithPasswordstate)}: {crd.Id}: detected changed password list in Passwordstate, will update password secret '{crd.Spec.SecretName}'");
                await kubernetesSdk.ReplaceSecretAsync(newPasswordsSecret, crd.Spec.SecretName, crd.Namespace());

                if (crd.Spec.AutoRestartDeploymentName != null)
                {
                    logger.LogInformation($"{nameof(SyncExistingPasswordSecretWithPasswordstate)}: {crd.Id}: will restart deployment '{crd.Spec.AutoRestartDeploymentName}'");
                    await kubernetesSdk.RestartDeployment(crd.Spec.AutoRestartDeploymentName, crd.Namespace());
                }
            }
        }

        private async Task CreatePasswordsSecret(PasswordListCrd crd)
        {
            var existingPasswordsSecret = await kubernetesSdk.GetSecretAsync(crd.Spec.SecretName, crd.Namespace());
            if (existingPasswordsSecret == null)
            {
                PasswordListResponse passwords;
                try
                {
                    passwords = await FetchPasswordListFromPasswordstate(crd);
                }
                catch (Exception e)
                {
                    logger.LogError(e, $"{nameof(CreatePasswordsSecret)}: {crd.Id}: Got exception, will not create password secret '{crd.Spec.SecretName}'");
                    return;
                }
            
                var passwordsSecret = BuildSecret(crd, passwords.Passwords);

                logger.LogInformation($"{nameof(CreatePasswordsSecret)}: {crd.Id}: will create password secret '{crd.Spec.SecretName}'");
                
                await kubernetesSdk.CreateSecretAsync(passwordsSecret, crd.Namespace());
            }
            else
            {
                await SyncExistingPasswordSecretWithPasswordstate(crd, existingPasswordsSecret);
            }
        }
        
        private async Task<PasswordListResponse> FetchPasswordListFromPasswordstate(PasswordListCrd crd)
        {
            return await passwordstateSdk.GetPasswordList(settings.ServerBaseUrl, crd.Spec.PasswordListId, await GetApiKey());
        }

        private async Task<string> GetApiKey()
        {
            if (apiKey == null)
            {
                apiKey = await File.ReadAllTextAsync(settings.ApiKeyPath);

                if (string.IsNullOrEmpty(apiKey))
                {
                    throw new ApplicationException($"{nameof(GetApiKey)}: api key file was empty '{settings.ApiKeyPath}'");
                }
            }

            return apiKey; 
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
                Metadata = new V1ObjectMeta(name: crd.Spec.SecretName),
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

            await kubernetesSdk.DeleteSecretAsync(existingCrd.Spec.SecretName, existingCrd.Namespace());
            await CreatePasswordsSecret(newCrd);
            
            if (newCrd.Spec.AutoRestartDeploymentName != null)
            {
                logger.LogInformation($"{nameof(UpdatePasswordsSecret)}: {newCrd.Id}: will restart deployment '{newCrd.Spec.AutoRestartDeploymentName}'");
                await kubernetesSdk.RestartDeployment(newCrd.Spec.AutoRestartDeploymentName, newCrd.Namespace());
            }
        }
    }
}