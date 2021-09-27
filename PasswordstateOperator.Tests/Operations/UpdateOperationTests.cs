using System.Threading.Tasks;
using k8s.Models;
using Moq;
using PasswordstateOperator.Kubernetes;
using PasswordstateOperator.Operations;
using Xunit;

namespace PasswordstateOperator.Tests.Operations
{
    public class UpdateOperationTests
    {
        [Fact]
        public async Task ShouldCreateNewSecret_WhenExistingSecretIsNull()
        {
            // Arrange
            var @namespace = "ns1";
            var passwordListId = "123";
            var secretName = "name1";
            
            var existingCrd = (PasswordListCrd) null;
            var newCrd = new PasswordListCrd
            {
                Metadata = new V1ObjectMeta { NamespaceProperty = @namespace},
                Spec = new Spec
                {
                    SecretName = secretName,
                    PasswordListId = passwordListId,
                    AutoRestartDeploymentName = null // Disable feature
                }
            };

            var kubernetesSdk = new Mock<IKubernetesSdk>(MockBehavior.Loose);
            
            var deleteOperation = new Mock<IDeleteOperation>(MockBehavior.Loose);
            var createOperation = new Mock<ICreateOperation>(MockBehavior.Loose);
            
            var operation = new UpdateOperation(
                new FakeLogger<UpdateOperation>(),
                kubernetesSdk.Object,
                deleteOperation.Object,
                createOperation.Object);
            
            // Act
            await operation.Update(existingCrd, newCrd);

            // Assert
            createOperation.Verify(
                op => op.Create(newCrd), 
                Times.Once);

            deleteOperation.Verify(
                sdk => sdk.Delete(It.IsAny<PasswordListCrd>()),
                Times.Never);

            kubernetesSdk.Verify(
                sdk => sdk.RestartDeploymentAsync(It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }
        
        [Fact]
        public async Task ShouldDoNothing_WhenSameSpec()
        {
            // Arrange
            var @namespace = "ns1";
            var passwordListId = "123";
            var secretName = "name1";
            
            var existingCrd = new PasswordListCrd
            {
                Metadata = new V1ObjectMeta { NamespaceProperty = @namespace},
                Spec = new Spec
                {
                    SecretName = secretName,
                    PasswordListId = passwordListId,
                    AutoRestartDeploymentName = null // Disable feature
                }
            };
            
            var newCrd = new PasswordListCrd
            {
                Metadata = new V1ObjectMeta { NamespaceProperty = @namespace},
                Spec = new Spec
                {
                    SecretName = secretName,
                    PasswordListId = passwordListId,
                    AutoRestartDeploymentName = null // Disable feature
                }
            };

            var kubernetesSdk = new Mock<IKubernetesSdk>(MockBehavior.Loose);
            
            var deleteOperation = new Mock<IDeleteOperation>(MockBehavior.Loose);
            var createOperation = new Mock<ICreateOperation>(MockBehavior.Loose);
            
            var operation = new UpdateOperation(
                new FakeLogger<UpdateOperation>(),
                kubernetesSdk.Object,
                deleteOperation.Object,
                createOperation.Object);
            
            // Act
            await operation.Update(existingCrd, newCrd);

            // Assert
            createOperation.Verify(
                op => op.Create(It.IsAny<PasswordListCrd>()), 
                Times.Never);

            deleteOperation.Verify(
                sdk => sdk.Delete(It.IsAny<PasswordListCrd>()),
                Times.Never);

            kubernetesSdk.Verify(
                sdk => sdk.RestartDeploymentAsync(It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }
        
        [Fact]
        public async Task ShouldDeleteAndCreate_WhenDifferentSpec()
        {
            // Arrange
            var @namespace = "ns1";
            var passwordListId = "123";
            var secretName = "name1";
            
            var existingCrd = new PasswordListCrd
            {
                Metadata = new V1ObjectMeta { NamespaceProperty = @namespace},
                Spec = new Spec
                {
                    SecretName = secretName,
                    PasswordListId = passwordListId,
                    AutoRestartDeploymentName = null // Disable feature
                }
            };
            
            var newCrd = new PasswordListCrd
            {
                Metadata = new V1ObjectMeta { NamespaceProperty = @namespace},
                Spec = new Spec
                {
                    SecretName = secretName,
                    PasswordListId = passwordListId + "NEW!",
                    AutoRestartDeploymentName = null // Disable feature
                }
            };

            var kubernetesSdk = new Mock<IKubernetesSdk>(MockBehavior.Loose);
            
            var deleteOperation = new Mock<IDeleteOperation>(MockBehavior.Loose);
            var createOperation = new Mock<ICreateOperation>(MockBehavior.Loose);
            
            var operation = new UpdateOperation(
                new FakeLogger<UpdateOperation>(),
                kubernetesSdk.Object,
                deleteOperation.Object,
                createOperation.Object);
            
            // Act
            await operation.Update(existingCrd, newCrd);

            // Assert
            createOperation.Verify(
                op => op.Create(newCrd), 
                Times.Once);

            deleteOperation.Verify(
                sdk => sdk.Delete(existingCrd),
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
            
            var existingCrd = new PasswordListCrd
            {
                Metadata = new V1ObjectMeta { NamespaceProperty = @namespace},
                Spec = new Spec
                {
                    SecretName = secretName,
                    PasswordListId = passwordListId,
                    AutoRestartDeploymentName = null // Disable feature
                }
            };

            var deployment = "deployment123";
            var newCrd = new PasswordListCrd
            {
                Metadata = new V1ObjectMeta { NamespaceProperty = @namespace},
                Spec = new Spec
                {
                    SecretName = secretName,
                    PasswordListId = passwordListId + "NEW!",
                    AutoRestartDeploymentName = deployment // Enable feature
                }
            };

            var kubernetesSdk = new Mock<IKubernetesSdk>(MockBehavior.Loose);
            
            var deleteOperation = new Mock<IDeleteOperation>(MockBehavior.Loose);
            var createOperation = new Mock<ICreateOperation>(MockBehavior.Loose);
            
            var operation = new UpdateOperation(
                new FakeLogger<UpdateOperation>(),
                kubernetesSdk.Object,
                deleteOperation.Object,
                createOperation.Object);
            
            // Act
            await operation.Update(existingCrd, newCrd);

            // Assert
            createOperation.Verify(
                op => op.Create(newCrd), 
                Times.Once);

            deleteOperation.Verify(
                sdk => sdk.Delete(existingCrd),
                Times.Once);

            kubernetesSdk.Verify(
                sdk => sdk.RestartDeploymentAsync(deployment, @namespace),
                Times.Once);
        }
    }
}