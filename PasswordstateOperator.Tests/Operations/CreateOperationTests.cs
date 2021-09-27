using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using k8s.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Rest;
using Moq;
using PasswordstateOperator.Kubernetes;
using PasswordstateOperator.Operations;
using PasswordstateOperator.Passwordstate;
using Xunit;

namespace PasswordstateOperator.Tests.Operations
{
    public class CreateOperationTests
    {
        [Fact]
        public async Task ShouldCreateSecret_WhenNotExists()
        {
            // Arrange
            var @namespace = "ns1";
            var passwordListId = "123";
            var secretName = "name1";
            var crd = new PasswordListCrd
            {
                Metadata = new V1ObjectMeta { NamespaceProperty = @namespace},
                Spec = new Spec
                {
                    SecretName = secretName,
                    PasswordListId = passwordListId
                }
            };

            var apiKey = "apikey123456";
            var passwordstateSdk = new Mock<IPasswordstateSdk>(MockBehavior.Loose);
            passwordstateSdk
                .Setup(sdk => sdk.GetPasswordList(It.IsAny<string>(), passwordListId, apiKey))
                .ReturnsAsync(GetPasswordListResponse());
            
            var kubernetesSdk = new Mock<IKubernetesSdk>(MockBehavior.Loose);
            
            var settings = new OptionsWrapper<Settings>(new Settings {ApiKey = apiKey});

            var getOperation = new Mock<IGetOperation>(MockBehavior.Loose);
            getOperation
                .Setup(op => op.Get(crd))
                .ReturnsAsync((V1Secret) null);
            
            var syncOperation = new Mock<ISyncOperation>(MockBehavior.Loose);
            
            var operation = new CreateOperation(
                new FakeLogger<CreateOperation>(),
                passwordstateSdk.Object,
                kubernetesSdk.Object,
                GetSecretsBuilder(),
                settings,
                getOperation.Object,
                syncOperation.Object);
            
            // Act
            await operation.Create(crd);

            // Assert
            kubernetesSdk.Verify(
                sdk => sdk.CreateSecretAsync(It.Is<V1Secret>(secret => secret.StringData["db.username"] == "username"), @namespace), 
                Times.Once);

            syncOperation.Verify(
                op => op.Sync(It.IsAny<PasswordListCrd>(), It.IsAny<V1Secret>()), 
                Times.Never);
        }
        
        [Fact]
        public async Task ShouldSyncSecret_WhenAlreadyExists()
        {
            // Arrange
            var @namespace = "ns1";
            var passwordListId = "123";
            var secretName = "name1";
            var crd = new PasswordListCrd
            {
                Metadata = new V1ObjectMeta { NamespaceProperty = @namespace},
                Spec = new Spec
                {
                    SecretName = secretName,
                    PasswordListId = passwordListId
                }
            };

            var apiKey = "apikey123456";
            var passwordstateSdk = new Mock<IPasswordstateSdk>(MockBehavior.Loose);
            passwordstateSdk
                .Setup(sdk => sdk.GetPasswordList(It.IsAny<string>(), passwordListId, apiKey))
                .ReturnsAsync(GetPasswordListResponse());
            
            var kubernetesSdk = new Mock<IKubernetesSdk>(MockBehavior.Loose);
            
            var settings = new OptionsWrapper<Settings>(new Settings {ApiKey = apiKey});

            var existingSecret = new V1Secret {Metadata = new V1ObjectMeta {Name = secretName, NamespaceProperty = @namespace}};
            var getOperation = new Mock<IGetOperation>(MockBehavior.Loose);
            getOperation
                .Setup(op => op.Get(crd))
                .ReturnsAsync(existingSecret);

            var syncOperation = new Mock<ISyncOperation>(MockBehavior.Loose);
            
            var operation = new CreateOperation(
                new FakeLogger<CreateOperation>(),
                passwordstateSdk.Object,
                kubernetesSdk.Object,
                GetSecretsBuilder(),
                settings,
                getOperation.Object,
                syncOperation.Object);
            
            // Act
            await operation.Create(crd);

            // Assert
            kubernetesSdk.Verify(
                sdk => sdk.CreateSecretAsync(It.IsAny<V1Secret>(), It.IsAny<string>()), 
                Times.Never);

            syncOperation.Verify(
                op => op.Sync(crd, existingSecret), 
                Times.Once);
        }
        
        [Fact]
        public async Task ShouldNotCreateSecret_WhenNotExists_AndFailureToFetchFromPasswordstate()
        {
            // Arrange
            var @namespace = "ns1";
            var passwordListId = "123";
            var secretName = "name1";
            var crd = new PasswordListCrd
            {
                Metadata = new V1ObjectMeta { NamespaceProperty = @namespace},
                Spec = new Spec
                {
                    SecretName = secretName,
                    PasswordListId = passwordListId
                }
            };

            var apiKey = "apikey123456";
            var passwordstateSdk = new Mock<IPasswordstateSdk>(MockBehavior.Loose);
            passwordstateSdk
                .Setup(sdk => sdk.GetPasswordList(It.IsAny<string>(), passwordListId, apiKey))
                .Throws<HttpOperationException>();
            
            var kubernetesSdk = new Mock<IKubernetesSdk>(MockBehavior.Loose);
            
            var settings = new OptionsWrapper<Settings>(new Settings {ApiKey = apiKey});

            var getOperation = new Mock<IGetOperation>(MockBehavior.Loose);
            getOperation
                .Setup(op => op.Get(crd))
                .ReturnsAsync((V1Secret) null);

            var syncOperation = new Mock<ISyncOperation>(MockBehavior.Loose);

            var logger = new FakeLogger<CreateOperation>();
            
            var operation = new CreateOperation(
                logger,
                passwordstateSdk.Object,
                kubernetesSdk.Object,
                GetSecretsBuilder(),
                settings,
                getOperation.Object,
                syncOperation.Object);
            
            // Act
            await operation.Create(crd);

            // Assert
            kubernetesSdk.Verify(
                sdk => sdk.CreateSecretAsync(It.IsAny<V1Secret>(), It.IsAny<string>()), 
                Times.Never);

            syncOperation.Verify(
                op => op.Sync(It.IsAny<PasswordListCrd>(), It.IsAny<V1Secret>()), 
                Times.Never);

            var errors = logger.Messages.Where(m => m.level == LogLevel.Error);
            Assert.Single(errors);
        }
        
        private static SecretsBuilder GetSecretsBuilder()
        {
            return new(new FakeLogger<SecretsBuilder>());
        }

        private PasswordListResponse GetPasswordListResponse()
        {
            return new()
            {
                Passwords = new List<Password>
                {
                    new()
                    {
                        Fields = new List<Field>
                        {
                            new()
                            {
                                Name = "PasswordID",
                                Value = "1"
                            },
                            new()
                            {
                                Name = "Title",
                                Value = "DB"
                            },
                            new()
                            {
                                Name = "UserName",
                                Value = "username"
                            },
                        }
                    }
                }
            };
        }
    }
}