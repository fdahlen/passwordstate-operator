using System;
using System.IO;
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
        private readonly SecretsBuilder secretsBuilder;
        private readonly Settings settings;

        private DateTimeOffset previousSyncTime = DateTimeOffset.MinValue;

        public OperationHandler(
            ILogger<OperationHandler> logger,
            PasswordstateSdk passwordstateSdk,
            IKubernetesSdk kubernetesSdk,
            SecretsBuilder secretsBuilder,
            IOptions<Settings> passwordstateSettings)
        {
            this.logger = logger;
            this.passwordstateSdk = passwordstateSdk;
            this.kubernetesSdk = kubernetesSdk;
            this.secretsBuilder = secretsBuilder;
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
                try
                {
                    await CheckCurrentStateForCrd(crd, sync);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, $"{nameof(CheckCurrentState)}: {crd.Id}: Failure");
                }
            }

            if (sync)
            {
                previousSyncTime = DateTimeOffset.UtcNow;
            }
        }

        private async Task CheckCurrentStateForCrd(PasswordListCrd crd, bool sync)
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

        private async Task SyncExistingPasswordSecretWithPasswordstate(PasswordListCrd crd, V1Secret existingPasswordsSecret)
        {
            var newPasswords = await passwordstateSdk.GetPasswordList(
                settings.ServerBaseUrl,
                crd.Spec.PasswordListId,
                await GetApiKey());

            var newPasswordsSecret = secretsBuilder.BuildPasswordsSecret(crd, newPasswords.Passwords);

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
                    passwords = await passwordstateSdk.GetPasswordList(
                        settings.ServerBaseUrl,
                        crd.Spec.PasswordListId,
                        await GetApiKey());
                }
                catch (Exception e)
                {
                    logger.LogError(e, $"{nameof(CreatePasswordsSecret)}: {crd.Id}: Got exception, will not create password secret '{crd.Spec.SecretName}'");
                    return;
                }

                var passwordsSecret = secretsBuilder.BuildPasswordsSecret(crd, passwords.Passwords);

                logger.LogInformation($"{nameof(CreatePasswordsSecret)}: {crd.Id}: will create password secret '{crd.Spec.SecretName}'");

                await kubernetesSdk.CreateSecretAsync(passwordsSecret, crd.Namespace());
            }
            else
            {
                await SyncExistingPasswordSecretWithPasswordstate(crd, existingPasswordsSecret);
            }
        }

        private async Task<string> GetApiKey()
        {
            var apiKey = await File.ReadAllTextAsync(settings.ApiKeyPath);

            if (string.IsNullOrEmpty(apiKey))
            {
                throw new ApplicationException($"{nameof(GetApiKey)}: api key file was empty '{settings.ApiKeyPath}'");
            }

            return apiKey;
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