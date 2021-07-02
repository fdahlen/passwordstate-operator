using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Rest;
using PasswordstateOperator.Passwordstate;

namespace PasswordstateOperator
{
    public class OperationHandler
    {
        private const int PasswordstateSyncIntervalSeconds = 60;
        private DateTimeOffset previousSyncTime = DateTimeOffset.UtcNow;
        
        private readonly ILogger<OperationHandler> logger;
        private readonly State currentState = new();
        private readonly PasswordstateSdk passwordstateSdk;

        public OperationHandler(ILogger<OperationHandler> logger, PasswordstateSdk passwordstateSdk)
        {
            this.logger = logger;
            this.passwordstateSdk = passwordstateSdk;
        }

        public async Task OnAdded(IKubernetes k8s, PasswordListCrd crd)
        {
            logger.LogInformation($"{nameof(OnAdded)}: {crd.Id}");

            await currentState.GuardedRun(crd.Id, async () => await CreatePasswordsSecret(k8s, crd));
        }

        public Task OnBookmarked(IKubernetes k8s, PasswordListCrd crd)
        {
             logger.LogInformation($"{nameof(OnBookmarked)}: {crd.Id}");

            return Task.CompletedTask;
        }

        public async Task OnDeleted(IKubernetes k8s, PasswordListCrd crd)
        {
            logger.LogInformation($"{nameof(OnDeleted)}: {crd.Id}");

            await currentState.GuardedRun(crd.Id, async () => await DeletePasswordsSecret(k8s, crd));
        }
        
        private async Task DeletePasswordsSecret(IKubernetes k8s, PasswordListCrd crd)
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

            if (!currentState.TryRemove(crd.Id))
            {
                throw new ApplicationException($"Failed to remove from state: {crd.Id}");
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

            await currentState.GuardedRun(newCrd.Id, async () => await UpdatePasswordsSecretIfRequired(k8s, newCrd));
        }

        public async Task CheckCurrentState(IKubernetes k8s)
        {
            logger.LogInformation(nameof(CheckCurrentState));

            foreach (var id in currentState.GetIds())
            {
                await currentState.GuardedRun(id, async () => await CheckCurrentStateForCrd(k8s, id));
            }
        }
        
        private async Task CheckCurrentStateForCrd(IKubernetes k8s, string id)
        {
            if (!currentState.TryGet(id, out var state))
            {
                logger.LogWarning($"{nameof(CheckCurrentStateForCrd)}: {id}: expected existing crd but none found, will skip");
                return;
            }

            var crd = state.Crd;
            
            //TODO: should sync also detect changes in Spec?

            var passwordsSecret = await GetPasswordsSecret(k8s, crd);
            if (passwordsSecret == null)
            {
                logger.LogDebug($"{nameof(CheckCurrentStateForCrd)}: {crd.Id}: does not exist, will create");

                await CreatePasswordsSecret(k8s, crd);
            }
            else
            {
                logger.LogDebug($"{nameof(CheckCurrentStateForCrd)}: {crd.Id}: exists");

                if (DateTimeOffset.UtcNow > previousSyncTime.AddSeconds(PasswordstateSyncIntervalSeconds))
                {
                    previousSyncTime = DateTimeOffset.UtcNow;

                    logger.LogDebug($"{nameof(CheckCurrentStateForCrd)}: {crd.Id}: {PasswordstateSyncIntervalSeconds}s has passed, will sync with Passwordstate");

                    await SyncWithPasswordstate(k8s, crd, state.PasswordsHashCode);
                }
            }
        }

        private async Task SyncWithPasswordstate(IKubernetes k8s, PasswordListCrd crd, int currentHashCode)
        {
            var (newPasswords, newHashCode) = await FetchPasswordListFromPasswordstate(k8s, crd);

            if (newHashCode != currentHashCode)
            {
                var newPasswordsSecret = CreateSecretFromResponse(crd, newPasswords);

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

        private async Task CreatePasswordsSecret(IKubernetes k8s, PasswordListCrd crd)
        {
            PasswordListResponse passwords;
            int hashCode;
            
            try
            {
                (passwords, hashCode) = await FetchPasswordListFromPasswordstate(k8s, crd);
            }
            catch (Exception e)
            {
                logger.LogError(e, $"{nameof(CreatePasswordsSecret)}: {crd.Id}: Got exception, will not create secret '{crd.Spec.PasswordsSecret}'");
                currentState.Add(crd.Id, new State.Entry(crd, 0));
                return;
            }

            var passwordsSecret = CreateSecretFromResponse(crd, passwords);
            await k8s.CreateNamespacedSecretAsync(passwordsSecret, crd.Namespace());

            currentState.Add(crd.Id, new State.Entry(crd, hashCode));

            logger.LogInformation($"{nameof(CreatePasswordsSecret)}: {crd.Id}: created '{crd.Spec.PasswordsSecret}'");
        }
        
        private async Task<(PasswordListResponse passwords, int hashCode)> FetchPasswordListFromPasswordstate(IKubernetes k8s, PasswordListCrd crd)
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

            var result = await passwordstateSdk.GetPasswordList(crd.Spec.ServerBaseUrl, crd.Spec.PasswordListId, apiKey);

            return (JsonSerializer.Deserialize<PasswordListResponse>(result.Content), result.Content.GetHashCode());
        }

        private V1Secret CreateSecretFromResponse(PasswordListCrd crd, PasswordListResponse passwordListResponse)
        {
            var passwords = new Dictionary<string, string>();

            foreach (var password in passwordListResponse)
            {
                const string TitleField = "Title";
                password.TryGetValue(TitleField, out var title);

                if (string.IsNullOrWhiteSpace(title))
                {
                    password.TryGetValue("PasswordID", out var passwordId);
                    logger.LogWarning($"{nameof(CreateSecretFromResponse)}: {crd.Id}: No {TitleField} found, skipping password ID {passwordId} in list ID {crd.Spec.PasswordListId}");
                    continue;
                }

                foreach (var (field, value) in password)
                {
                    if (field == TitleField)
                    {
                        continue;
                    }

                    var stringValue = value.ToString();
                    if (string.IsNullOrEmpty(stringValue))
                    {
                        continue;
                    }
                    
                    var key = Clean($"{title}.{field}");                    
                    passwords[key] = stringValue;
                }
            }

            return new V1Secret
            {
                ApiVersion = "v1",
                Kind = "Secret",
                Metadata = new V1ObjectMeta(name: crd.Spec.PasswordsSecret),
                StringData = passwords
            };
        }
        
        private static string Clean(string secretKey)
        {
            return Regex.Replace(secretKey, "[^A-Za-z0-9_-.]", "").ToLower();
        }

        private async Task UpdatePasswordsSecretIfRequired(IKubernetes k8s, PasswordListCrd newCrd)
        {
            if (!currentState.TryGet(newCrd.Id, out var state))
            {
                logger.LogWarning($"{nameof(OnUpdated)}: {newCrd.Id}: expected existing crd but none found, will create new");
                await CreatePasswordsSecret(k8s, newCrd);
                return;
            }
            
            var currentCrd = state.Crd;
            
            if (currentCrd.Spec.ToString() == newCrd.Spec.ToString())
            {
                return;
            }

            logger.LogInformation($"{nameof(OnUpdated)}: {newCrd.Id}: detected updated crd, will delete existing and create new");

            await DeletePasswordsSecret(k8s, currentCrd);
            await CreatePasswordsSecret(k8s, newCrd);
        }
    }
}