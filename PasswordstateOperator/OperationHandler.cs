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
using RestSharp;

namespace PasswordstateOperator
{
    public class OperationHandler
    {
        private const int PasswordstateSyncIntervalSeconds = 60;
        private DateTimeOffset previousSyncTime = DateTimeOffset.UtcNow;
        
        private readonly ILogger<OperationHandler> logger;
        private readonly State currentState = new();

        public OperationHandler(ILogger<OperationHandler> logger)
        {
            this.logger = logger;
        }

        public async Task OnAdded(Kubernetes k8s, PasswordListCrd crd)
        {
            logger.LogInformation($"{nameof(OnAdded)}: {crd.ID}");

            await currentState.GuardedRun(crd.ID, async () => await CreatePasswordsSecret(k8s, crd));
        }

        public Task OnBookmarked(Kubernetes k8s, PasswordListCrd crd)
        {
             logger.LogInformation($"{nameof(OnBookmarked)}: {crd.ID}");

            return Task.CompletedTask;
        }

        public async Task OnDeleted(Kubernetes k8s, PasswordListCrd crd)
        {
            logger.LogInformation($"{nameof(OnDeleted)}: {crd.ID}");

            await currentState.GuardedRun(crd.ID, async () => await DeletePasswordsSecret(k8s, crd));
        }
        
        private async Task DeletePasswordsSecret(Kubernetes k8s, PasswordListCrd crd)
        {
            await k8s.DeleteNamespacedSecretAsync(crd.Spec.PasswordsSecret, crd.Namespace());
            
            logger.LogInformation($"{nameof(DeletePasswordsSecret)}: {crd.ID}: deleted '{crd.Spec.PasswordsSecret}'");

            if (!currentState.TryRemove(crd.ID))
            {
                throw new ApplicationException($"Failed to remove from state: {crd.ID}");
            }
        }

        public Task OnError(Kubernetes k8s, PasswordListCrd crd)
        {
            logger.LogError($"{nameof(OnError)}: {crd.ID}");

            return Task.CompletedTask;
        }

        public async Task OnUpdated(Kubernetes k8s, PasswordListCrd newCrd)
        {
            logger.LogInformation($"{nameof(OnUpdated)}: {newCrd.ID}");

            await currentState.GuardedRun(newCrd.ID, async () => await UpdatePasswordsSecretIfRequired(k8s, newCrd));
        }

        public async Task CheckCurrentState(Kubernetes k8s)
        {
            logger.LogInformation(nameof(CheckCurrentState));

            foreach (var id in currentState.GetIds())
            {
                await currentState.GuardedRun(id, async () => await CheckCurrentStateForCrd(k8s, id));
            }
        }
        
        private async Task CheckCurrentStateForCrd(Kubernetes k8s, string id)
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
                logger.LogDebug($"{nameof(CheckCurrentStateForCrd)}: {crd.ID}: does not exist, will create");

                await CreatePasswordsSecret(k8s, crd);
            }
            else
            {
                logger.LogDebug($"{nameof(CheckCurrentStateForCrd)}: {crd.ID}: exists");

                if (DateTimeOffset.UtcNow > previousSyncTime.AddSeconds(PasswordstateSyncIntervalSeconds))
                {
                    previousSyncTime = DateTimeOffset.UtcNow;

                    logger.LogDebug($"{nameof(CheckCurrentStateForCrd)}: {crd.ID}: {PasswordstateSyncIntervalSeconds}s has passed, will sync with Passwordstate");

                    await SyncWithPasswordstate(k8s, crd, state.PasswordsHashCode);
                }
            }
        }

        private async Task SyncWithPasswordstate(Kubernetes k8s, PasswordListCrd crd, int currentHashCode)
        {
            var (newPasswords, newHashCode) = await FetchPasswordListFromPasswordstate(k8s, crd);

            if (newHashCode != currentHashCode)
            {
                var newPasswordsSecret = CreateFrom(newPasswords, crd.Spec.PasswordsSecret);

                logger.LogInformation($"{nameof(SyncWithPasswordstate)}: {crd.ID}: detected changed password list in Passwordstate, will update password secret '{crd.Spec.PasswordsSecret}'");

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

        private async Task CreatePasswordsSecret(Kubernetes k8s, PasswordListCrd crd)
        {
            List<PasswordstatePassword> passwords;
            int hashCode;
            
            try
            {
                (passwords, hashCode) = await FetchPasswordListFromPasswordstate(k8s, crd);
            }
            catch (Exception e)
            {
                logger.LogError(e, $"{nameof(CreatePasswordsSecret)}: {crd.ID}: Got exception, will not create secret '{crd.Spec.PasswordsSecret}'");
                return;
            }

            var passwordsSecret = CreateFrom(passwords, crd.Spec.PasswordsSecret);
            await k8s.CreateNamespacedSecretAsync(passwordsSecret, crd.Namespace());

            currentState.Add(crd.ID, new State.Entry(crd, hashCode));

            logger.LogInformation($"{nameof(CreatePasswordsSecret)}: {crd.ID}: created '{crd.Spec.PasswordsSecret}'");
        }
        
        private static async Task<(List<PasswordstatePassword> passwords, int hashCode)> FetchPasswordListFromPasswordstate(Kubernetes k8s, PasswordListCrd crd)
        {
            V1Secret apiKeySecret;
            try
            {
                apiKeySecret = await k8s.ReadNamespacedSecretAsync(crd.Spec.ApiKeySecret, crd.Namespace());
            }
            catch (HttpOperationException hoex) when (hoex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new ApplicationException($"{nameof(CreatePasswordsSecret)}: {crd.ID}: api key secret was '{crd.Spec.ApiKeySecret}' not found", hoex);
            }

            const string dataName = "apikey";
            if (!apiKeySecret.Data.TryGetValue(dataName, out var apiKeyBytes))
            {
                throw new ApplicationException($"{nameof(CreatePasswordsSecret)}: {crd.ID}: data field '{dataName}' was not found in api key secret '{crd.Spec.ApiKeySecret}' ");
            }

            var apiKey = Encoding.UTF8.GetString(apiKeyBytes);

            var restClient = new RestClient(crd.Spec.ServerBaseUrl);
            var restRequest = new RestRequest($"/api/passwords/{crd.Spec.PasswordListID}", Method.GET);
            restRequest.AddHeader("APIKey", apiKey);
            restRequest.AddQueryParameter("QueryAll", "true");

            var result = await restClient.ExecuteAsync(restRequest);

            if (!result.IsSuccessful)
            {
                throw new ApplicationException($"Failed to fetch password list with id {crd.Spec.PasswordListID} from Passwordstate: {result.ErrorMessage} {result.ErrorException}");
            }

            return (JsonSerializer.Deserialize<List<PasswordstatePassword>>(result.Content), result.Content.GetHashCode());
        }

        private static V1Secret CreateFrom(List<PasswordstatePassword> data, string secretName)
        {
            var passwords = new Dictionary<string, string>();

            foreach (var password in data)
            {
                if (!string.IsNullOrWhiteSpace(password.Password))
                {
                    var key = Clean($"{password.Title}.{nameof(password.Password)}");
                    passwords[key] = password.Password;
                }
                
                if (!string.IsNullOrWhiteSpace(password.UserName))
                {
                    var key = Clean($"{password.Title}.{nameof(password.UserName)}");
                    passwords[key] = password.UserName;
                }
                
                //TODO: rest of fields, maybe reflection based?
            }

            return new V1Secret
            {
                ApiVersion = "v1",
                Kind = "Secret",
                Metadata = new V1ObjectMeta(name: secretName),
                StringData = passwords
            };
        }
        
        private static string Clean(string secretKey)
        {
            return Regex.Replace(secretKey, "[^A-Za-z0-9_-.]", "").ToLower();
        }

        private async Task UpdatePasswordsSecretIfRequired(Kubernetes k8s, PasswordListCrd newCrd)
        {
            if (!currentState.TryGet(newCrd.ID, out var state))
            {
                logger.LogWarning($"{nameof(OnUpdated)}: {newCrd.ID}: expected existing crd but none found, will create new");
                await CreatePasswordsSecret(k8s, newCrd);
                return;
            }
            
            var currentCrd = state.Crd;
            
            if (currentCrd.Spec.ToString() == newCrd.Spec.ToString())
            {
                return;
            }

            logger.LogInformation($"{nameof(OnUpdated)}: {newCrd.ID}: detected updated crd, will delete existing and create new");

            await DeletePasswordsSecret(k8s, currentCrd);
            await CreatePasswordsSecret(k8s, newCrd);
        }
    }
}