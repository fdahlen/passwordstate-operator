using System.Threading.Tasks;
using k8s.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PasswordstateOperator.Kubernetes;
using PasswordstateOperator.Passwordstate;

namespace PasswordstateOperator.Operations
{
    public class SyncOperation : ISyncOperation
    {
        private readonly ILogger<SyncOperation> logger;
        private readonly IPasswordstateSdk passwordstateSdk;
        private readonly IKubernetesSdk kubernetesSdk;
        private readonly SecretsBuilder secretsBuilder;
        private readonly Settings settings;
        
        public SyncOperation(ILogger<SyncOperation> logger, IPasswordstateSdk passwordstateSdk, IKubernetesSdk kubernetesSdk, SecretsBuilder secretsBuilder, IOptions<Settings> settings)
        {
            this.logger = logger;
            this.passwordstateSdk = passwordstateSdk;
            this.kubernetesSdk = kubernetesSdk;
            this.secretsBuilder = secretsBuilder;
            this.settings = settings.Value;
        }

        public async Task Sync(PasswordListCrd crd, V1Secret existingPasswordsSecret)
        {
            logger.LogDebug($"{nameof(Sync)}: {crd.Id}: will sync password secret '{crd.Spec.SecretName}'");

            var newPasswords = await passwordstateSdk.GetPasswordList(
                settings.ServerBaseUrl,
                crd.Spec.PasswordListId,
                await settings.GetApiKey());

            var newPasswordsSecret = secretsBuilder.BuildPasswordsSecret(crd, newPasswords.Passwords);

            if (existingPasswordsSecret.DataEquals(newPasswordsSecret))
            {
                logger.LogDebug($"{nameof(Sync)}: {crd.Id}: no changes in Passwordstate, will skip password secret '{crd.Spec.SecretName}'");
            }
            else
            {
                logger.LogInformation($"{nameof(Sync)}: {crd.Id}: detected changed password list in Passwordstate, will update password secret '{crd.Spec.SecretName}'");
                await kubernetesSdk.ReplaceSecretAsync(newPasswordsSecret, crd.Spec.SecretName, crd.Namespace());

                if (crd.Spec.AutoRestartDeploymentName != null)
                {
                    logger.LogInformation($"{nameof(Sync)}: {crd.Id}: will restart deployment '{crd.Spec.AutoRestartDeploymentName}'");
                    await kubernetesSdk.RestartDeploymentAsync(crd.Spec.AutoRestartDeploymentName, crd.Namespace());
                }
            }
        }
    }
}