using System;
using System.Threading.Tasks;
using k8s.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PasswordstateOperator.Kubernetes;
using PasswordstateOperator.Passwordstate;

namespace PasswordstateOperator.Operations
{
    public class CreateOperation : ICreateOperation
    {
        private readonly ILogger<CreateOperation> logger;
        private readonly IPasswordstateSdk passwordstateSdk;
        private readonly IKubernetesSdk kubernetesSdk;
        private readonly SecretsBuilder secretsBuilder;
        private readonly Settings settings;
        private readonly IGetOperation getOperation;
        private readonly ISyncOperation syncOperation;
        
        public CreateOperation(ILogger<CreateOperation> logger, IPasswordstateSdk passwordstateSdk, IKubernetesSdk kubernetesSdk, SecretsBuilder secretsBuilder, IOptions<Settings> settings, IGetOperation getOperation, ISyncOperation syncOperation)
        {
            this.logger = logger;
            this.passwordstateSdk = passwordstateSdk;
            this.kubernetesSdk = kubernetesSdk;
            this.secretsBuilder = secretsBuilder;
            this.getOperation = getOperation;
            this.syncOperation = syncOperation;
            this.settings = settings.Value;
        }

        public async Task Create(PasswordListCrd crd)
        {
            var existingPasswordsSecret = await getOperation.Get(crd);
            if (existingPasswordsSecret == null)
            {
                PasswordListResponse passwords;
                try
                {
                    passwords = await passwordstateSdk.GetPasswordList(
                        settings.ServerBaseUrl,
                        crd.Spec.PasswordListId,
                        await settings.GetApiKey());
                }
                catch (Exception e)
                {
                    logger.LogError(e, $"{nameof(Create)}: {crd.Id}: Got exception, will not create password secret '{crd.Spec.SecretName}'");
                    return;
                }

                var passwordsSecret = secretsBuilder.BuildPasswordsSecret(crd, passwords.Passwords);

                logger.LogInformation($"{nameof(Create)}: {crd.Id}: will create password secret '{crd.Spec.SecretName}'");

                await kubernetesSdk.CreateSecretAsync(passwordsSecret, crd.Namespace());
            }
            else
            {
                await syncOperation.Sync(crd, existingPasswordsSecret);
            }
        }
    }
}