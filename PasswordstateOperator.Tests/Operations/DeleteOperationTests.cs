using System.Threading.Tasks;
using k8s.Models;
using Moq;
using PasswordstateOperator.Kubernetes;
using PasswordstateOperator.Operations;
using Xunit;

namespace PasswordstateOperator.Tests.Operations
{
    public class DeleteOperationTests
    {
        [Fact]
        public async Task ShouldDeleteSecret()
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

            var kubernetesSdk = new Mock<IKubernetesSdk>(MockBehavior.Loose);
            
            var operation = new DeleteOperation(kubernetesSdk.Object);
            
            // Act
            await operation.Delete(crd);

            // Assert
            kubernetesSdk.Verify(
                sdk => sdk.DeleteSecretAsync(secretName, @namespace),
                Times.Once);
        }
   }
}