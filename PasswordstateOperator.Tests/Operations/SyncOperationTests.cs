using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using k8s.Models;
using Microsoft.Extensions.Options;
using Moq;
using PasswordstateOperator.Kubernetes;
using PasswordstateOperator.Operations;
using PasswordstateOperator.Passwordstate;
using Xunit;

namespace PasswordstateOperator.Tests.Operations
{
    public class SyncOperationTests
    {
        [Fact]
        public async Task ShouldNotReplaceSecret_WhenNoChange()
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
                    PasswordListId = passwordListId,
                    AutoRestartDeploymentName = null // Disable feature
                }
            };

            var existingSecret = new V1Secret { Data = new Dictionary<string, byte[]>
            {
                {"db.username", Encoding.UTF8.GetBytes("username")}
            }};
            
            var newPasswordListResponse = GetPasswordListResponse("username");

            var apiKey = "apikey123456";
            var passwordstateSdk = new Mock<IPasswordstateSdk>(MockBehavior.Loose);
            passwordstateSdk
                .Setup(sdk => sdk.GetPasswordList(It.IsAny<string>(), passwordListId, apiKey))
                .ReturnsAsync(newPasswordListResponse);
            
            var kubernetesSdk = new Mock<IKubernetesSdk>(MockBehavior.Loose);
            
            var settings = new OptionsWrapper<Settings>(new Settings {ApiKey = apiKey});

            var operation = new SyncOperation(
                new FakeLogger<SyncOperation>(),
                passwordstateSdk.Object,
                kubernetesSdk.Object,
                GetSecretsBuilder(),
                settings);
            
            // Act
            await operation.Sync(crd, existingSecret);

            // Assert
            kubernetesSdk.Verify(
                sdk => sdk.ReplaceSecretAsync(It.IsAny<V1Secret>(), It.IsAny<string>(), It.IsAny<string>()), 
                Times.Never);

            kubernetesSdk.Verify(
                sdk => sdk.RestartDeploymentAsync(It.IsAny<string>(), It.IsAny<string>()), 
                Times.Never);
        }
        
        [Fact]
        public async Task ShouldReplaceSecret_WhenDataChanged()
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
                    PasswordListId = passwordListId,
                    AutoRestartDeploymentName = null // Disable feature
                }
            };

            var existingSecret = new V1Secret { Data = new Dictionary<string, byte[]>
            {
                {"db.username", Encoding.UTF8.GetBytes("username")}
            }};
            
            var newPasswordListResponse = GetPasswordListResponse("new_username");

            var apiKey = "apikey123456";
            var passwordstateSdk = new Mock<IPasswordstateSdk>(MockBehavior.Loose);
            passwordstateSdk
                .Setup(sdk => sdk.GetPasswordList(It.IsAny<string>(), passwordListId, apiKey))
                .ReturnsAsync(newPasswordListResponse);
            
            var kubernetesSdk = new Mock<IKubernetesSdk>(MockBehavior.Loose);
            
            var settings = new OptionsWrapper<Settings>(new Settings {ApiKey = apiKey});

            var operation = new SyncOperation(
                new FakeLogger<SyncOperation>(),
                passwordstateSdk.Object,
                kubernetesSdk.Object,
                GetSecretsBuilder(),
                settings);
            
            // Act
            await operation.Sync(crd, existingSecret);

            // Assert
            kubernetesSdk.Verify(
                sdk => sdk.ReplaceSecretAsync(It.Is<V1Secret>(secret => secret.StringData["db.username"] == "new_username"), secretName, @namespace), 
                Times.Once);

            kubernetesSdk.Verify(
                sdk => sdk.RestartDeploymentAsync(It.IsAny<string>(), It.IsAny<string>()), 
                Times.Never);
        }
        
        [Fact]
        public async Task ShouldRestartDeployment_WhenFeatureEnabled()
        {
            // Arrange
            var @namespace = "ns1";
            var passwordListId = "123";
            var secretName = "name1";
            var deployment = "deployment123";
            var crd = new PasswordListCrd
            {
                Metadata = new V1ObjectMeta { NamespaceProperty = @namespace},
                Spec = new Spec
                {
                    SecretName = secretName,
                    PasswordListId = passwordListId,
                    AutoRestartDeploymentName = deployment // Enable feature
                }
            };

            var existingSecret = new V1Secret { Data = new Dictionary<string, byte[]>
            {
                {"db.username", Encoding.UTF8.GetBytes("username")}
            }};
            
            var newPasswordListResponse = GetPasswordListResponse("new_username");

            var apiKey = "apikey123456";
            var passwordstateSdk = new Mock<IPasswordstateSdk>(MockBehavior.Loose);
            passwordstateSdk
                .Setup(sdk => sdk.GetPasswordList(It.IsAny<string>(), passwordListId, apiKey))
                .ReturnsAsync(newPasswordListResponse);
            
            var kubernetesSdk = new Mock<IKubernetesSdk>(MockBehavior.Loose);
            
            var settings = new OptionsWrapper<Settings>(new Settings {ApiKey = apiKey});

            var operation = new SyncOperation(
                new FakeLogger<SyncOperation>(),
                passwordstateSdk.Object,
                kubernetesSdk.Object,
                GetSecretsBuilder(),
                settings);
            
            // Act
            await operation.Sync(crd, existingSecret);

            // Assert
            kubernetesSdk.Verify(
                sdk => sdk.ReplaceSecretAsync(It.Is<V1Secret>(secret => secret.StringData["db.username"] == "new_username"), secretName, @namespace), 
                Times.Once);

            kubernetesSdk.Verify(
                sdk => sdk.RestartDeploymentAsync(deployment, @namespace), 
                Times.Once);
        }
        
        private static SecretsBuilder GetSecretsBuilder()
        {
            return new(new FakeLogger<SecretsBuilder>());
        }

        private PasswordListResponse GetPasswordListResponse(string username = "username")
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
                                Value = username
                            },
                        }
                    }
                }
            };
        }
    }
}