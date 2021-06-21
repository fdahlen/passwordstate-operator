using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly Dictionary<string, PasswordListCrd> currentState = new();
        private const int PasswordstateSyncIntervalSeconds = 60;
        private DateTimeOffset previousSyncTime = DateTimeOffset.UtcNow;
        
        private readonly ILogger<OperationHandler> logger;
        
        public OperationHandler(ILogger<OperationHandler> logger)
        {
            this.logger = logger;
        }

        public Task OnAdded(Kubernetes k8s, PasswordListCrd crd)
        {
            logger.LogInformation($"{nameof(OnAdded)}: {crd.Namespace()}/{crd.Name()}");

            lock (currentState)
            {
                CreatePasswordsSecret(k8s, crd);
            }

            return Task.CompletedTask;
        }

        public Task OnBookmarked(Kubernetes k8s, PasswordListCrd crd)
        {
             logger.LogInformation($"{nameof(OnBookmarked)}: {crd.Namespace()}/{crd.Name()}");

            return Task.CompletedTask;
        }

        public Task OnDeleted(Kubernetes k8s, PasswordListCrd crd)
        {
            logger.LogInformation($"{nameof(OnDeleted)}: {crd.Namespace()}/{crd.Name()}");

            lock (currentState)
            {
                DeletePasswordsSecret(k8s, crd);

                currentState.Remove(crd.Name());

                return Task.CompletedTask;
            }
        }
        
        private void DeletePasswordsSecret(Kubernetes k8s, PasswordListCrd crd)
        {
            k8s.DeleteNamespacedSecret(crd.Spec.PasswordsSecret, crd.Namespace());
            
            logger.LogInformation($"{nameof(DeletePasswordsSecret)}: {crd.Namespace()}/{crd.Name()}: deleted '{crd.Spec.PasswordsSecret}'");
        }

        public Task OnError(Kubernetes k8s, PasswordListCrd crd)
        {
            logger.LogError($"{nameof(OnError)}: {crd.Name()}");

            return Task.CompletedTask;
        }

        public Task OnUpdated(Kubernetes k8s, PasswordListCrd newCrd)
        {
            logger.LogInformation($"{nameof(OnUpdated)}: {newCrd.Namespace()}/{newCrd.Name()}");

            lock (currentState)
            {
                var currentCrd = currentState[newCrd.Name()];
                currentState[newCrd.Name()] = newCrd;
                UpdatePasswordsSecretIfRequired(k8s, currentCrd, newCrd);
            }

            return Task.CompletedTask;
        }

        public Task CheckCurrentState(Kubernetes k8s)
        {
            logger.LogInformation(nameof(CheckCurrentState));

            lock (currentState)
            {
                foreach (var crd in currentState.Values)
                {
                    var passwordsSecret = GetPasswordsSecret(k8s, crd);
                    if (passwordsSecret == null)
                    {
                        logger.LogDebug($"{nameof(CheckCurrentState)}: {crd.Namespace()}/{crd.Name()}: does not exist, will create");
                        
                        CreatePasswordsSecret(k8s, crd);
                    }
                    else
                    {
                        logger.LogDebug($"{nameof(CheckCurrentState)}: {crd.Namespace()}/{crd.Name()}: exists");

                        if (DateTimeOffset.UtcNow > previousSyncTime.AddSeconds(PasswordstateSyncIntervalSeconds))
                        {
                            previousSyncTime = DateTimeOffset.UtcNow;
                            
                            logger.LogDebug($"{nameof(CheckCurrentState)}: {crd.Namespace()}/{crd.Name()}: {PasswordstateSyncIntervalSeconds}s has passed, will sync with Passwordstate");

                            SyncWithPasswordstate(k8s, crd, passwordsSecret);
                        }
                    }
                }
            }

            return Task.CompletedTask;
        }
        
        private void SyncWithPasswordstate(Kubernetes k8s, PasswordListCrd crd, V1Secret currentPasswordsSecret)
        {
            var data = FetchPasswordListFromPasswordstate(k8s, crd);
            var newPasswordsSecret = CreateFrom(data, crd.Spec.PasswordsSecret);

            var hasSameContents =
                newPasswordsSecret.Data
                    .OrderBy(kvp => kvp.Key)
                    .SequenceEqual(
                        currentPasswordsSecret.Data
                            .OrderBy(kvp => kvp.Key));
            
            if (!hasSameContents)
            {
                logger.LogInformation($"{nameof(CheckCurrentState)}: {crd.Namespace()}/{crd.Name()}: detected changed password list in Passwordstate, will update password secret '{crd.Spec.PasswordsSecret}'");

                k8s.ReplaceNamespacedSecret(newPasswordsSecret, crd.Spec.PasswordsSecret, crd.Namespace());
            }
        }

        private static V1Secret GetPasswordsSecret(IKubernetes k8s, PasswordListCrd crd)
        {
            try
            {
                return k8s.ReadNamespacedSecret(crd.Spec.PasswordsSecret, crd.Namespace());
            }
            catch (HttpOperationException hoex) when (hoex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        private void CreatePasswordsSecret(Kubernetes k8s, PasswordListCrd crd)
        {
            var data = FetchPasswordListFromPasswordstate(k8s, crd);
            var passwordsSecret = CreateFrom(data, crd.Spec.PasswordsSecret);
            k8s.CreateNamespacedSecret(passwordsSecret, crd.Namespace());
            currentState[crd.Name()] = crd;

            logger.LogInformation($"{nameof(CreatePasswordsSecret)}: {crd.Namespace()}/{crd.Name()}: created '{crd.Spec.PasswordsSecret}'");
        }
        
        private static List<PasswordstatePassword> FetchPasswordListFromPasswordstate(Kubernetes k8s, PasswordListCrd crd)
        {
            V1Secret apiKeySecret;
            try
            {
                apiKeySecret = k8s.ReadNamespacedSecret(crd.Spec.ApiKeySecret, crd.Namespace());
            }
            catch (HttpOperationException hoex) when (hoex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new ApplicationException($"{nameof(CreatePasswordsSecret)}: {crd.Namespace()}/{crd.Name()}: api key secret was '{crd.Spec.ApiKeySecret}' not found", hoex);
            }

            const string dataName = "apikey";
            if (!apiKeySecret.Data.TryGetValue(dataName, out var apiKeyBytes))
            {
                throw new ApplicationException($"{nameof(CreatePasswordsSecret)}: {crd.Namespace()}/{crd.Name()}: data field '{dataName}' was not found in api key secret '{crd.Spec.ApiKeySecret}' ");
            }

            var apiKey = Encoding.UTF8.GetString(apiKeyBytes);

            var restClient = new RestClient(crd.Spec.ServerBaseUrl);
            var restRequest = new RestRequest($"/api/passwords/{crd.Spec.PasswordListID}", Method.GET);
            restRequest.AddHeader("APIKey", apiKey);
            restRequest.AddQueryParameter("QueryAll", "true");

            var result = restClient.Execute(restRequest);

            if (!result.IsSuccessful)
            {
                throw new ApplicationException($"Failed to fetch password list with id {crd.Spec.PasswordListID} from Passwordstate: {result.ErrorMessage} {result.ErrorException}");
            }

            return JsonSerializer.Deserialize<List<PasswordstatePassword>>(result.Content);
        }

        private V1Secret CreateFrom(List<PasswordstatePassword> data, string secretName)
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

        private void UpdatePasswordsSecretIfRequired(Kubernetes k8s, PasswordListCrd currentCrd, PasswordListCrd newCrd)
        {
            if (currentCrd.Spec.PasswordsSecret == newCrd.Spec.PasswordsSecret)
            {
                return;
            }

            logger.LogDebug($"{nameof(OnUpdated)}: {newCrd.Namespace()}/{newCrd.Name()}: detected renamed password secret");

            CreatePasswordsSecret(k8s, newCrd);
            DeletePasswordsSecret(k8s, currentCrd);
        }
    }
}