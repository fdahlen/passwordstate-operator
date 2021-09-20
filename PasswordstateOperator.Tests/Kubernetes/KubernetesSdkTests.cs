using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.JsonPatch.Operations;
using Microsoft.Extensions.Logging;
using Microsoft.Rest;
using Moq;
using PasswordstateOperator.Kubernetes;
using Xunit;

namespace PasswordstateOperator.Tests.Kubernetes
{
    public class KubernetesSdkTests
    {
        [Fact]
        public void WatchCustomResources_ShouldListWithExpectedValues()
        {
            // Arrange
            var group = "group";
            var version = "version";
            var plural = "plurals";
            var allNamespaces = "";

            var kubernetes = new Mock<IKubernetes>(MockBehavior.Loose);
            var kubernetesFactory = new Mock<IKubernetesFactory>();
            kubernetesFactory
                .Setup(factory => factory.Create())
                .Returns(kubernetes.Object);

            var logger = new FakeLogger<KubernetesSdk>();

            var sdk = new KubernetesSdk(kubernetesFactory.Object, logger);

            // Act
            sdk.WatchCustomResources<object>(@group, version, plural, null, null, null);
 
            // Assert
            kubernetes.Verify(k => k.ListNamespacedCustomObjectWithHttpMessagesAsync(group, version, allNamespaces, plural, null, null, null, null, null, null, null, null, true, null,
                null, CancellationToken.None), Times.Once);
        }
        
        [Fact]
        public async Task GetSecretAsync_ShouldGetWithExpectedValues()
        {
            // Arrange
            var secretName = "secret123";
            var secret = new V1Secret {Metadata = new V1ObjectMeta {Name = secretName}};
            var @namespace = "namespace";

            var kubernetes = new Mock<IKubernetes>(MockBehavior.Strict);
            kubernetes.Setup(k => k.ReadNamespacedSecretWithHttpMessagesAsync(secretName, @namespace, null, null, CancellationToken.None))
                .ReturnsAsync(new HttpOperationResponse<V1Secret> {Body = secret})
                .Verifiable();
            
            var kubernetesFactory = new Mock<IKubernetesFactory>();
            kubernetesFactory
                .Setup(factory => factory.Create())
                .Returns(kubernetes.Object);

            var logger = new FakeLogger<KubernetesSdk>();

            var sdk = new KubernetesSdk(kubernetesFactory.Object, logger);

            // Act
            var result = await sdk.GetSecretAsync(secretName, @namespace);
 
            // Assert
            kubernetes.Verify();
            
            Assert.Same(secret, result);
        }
        
        [Fact]
        public async Task GetSecretAsync_ShouldLogAndRethrow_WhenException()
        {
            // Arrange
            var secretName = "secret123";
            var @namespace = "namespace456";

            var httpStatusCode = HttpStatusCode.InternalServerError;
            var originalException = new HttpOperationException { Response = new HttpResponseMessageWrapper(new HttpResponseMessage(httpStatusCode), "content")};
            var kubernetes = new Mock<IKubernetes>(MockBehavior.Strict);
            kubernetes.Setup(k => k.ReadNamespacedSecretWithHttpMessagesAsync(secretName, @namespace, null, null, CancellationToken.None))
                .Throws(originalException);
            
            var kubernetesFactory = new Mock<IKubernetesFactory>();
            kubernetesFactory
                .Setup(factory => factory.Create())
                .Returns(kubernetes.Object);

            var logger = new FakeLogger<KubernetesSdk>();

            var sdk = new KubernetesSdk(kubernetesFactory.Object, logger);

            // Act + first assert
            var result = await Assert.ThrowsAsync<HttpOperationException>(() => sdk.GetSecretAsync(secretName, @namespace));
 
            // Assert
            Assert.Same(originalException, result);
            Assert.Single(logger.Messages);
            Assert.Equal(LogLevel.Error, logger.Messages.Single().level);
            Assert.Contains(secretName, logger.Messages.Single().message);
            Assert.Contains(@namespace, logger.Messages.Single().message);
        }

        [Fact]
        public async Task GetSecretAsync_ShouldReturnNull_WhenNotFound()
        {
            // Arrange
            var secretName = "secret123";
            var @namespace = "namespace456";

            var httpStatusCode = HttpStatusCode.NotFound;
            var originalException = new HttpOperationException { Response = new HttpResponseMessageWrapper(new HttpResponseMessage(httpStatusCode), "content")};
            var kubernetes = new Mock<IKubernetes>(MockBehavior.Strict);
            kubernetes.Setup(k => k.ReadNamespacedSecretWithHttpMessagesAsync(secretName, @namespace, null, null, CancellationToken.None))
                .Throws(originalException)
                .Verifiable();
            
            var kubernetesFactory = new Mock<IKubernetesFactory>();
            kubernetesFactory
                .Setup(factory => factory.Create())
                .Returns(kubernetes.Object);

            var logger = new FakeLogger<KubernetesSdk>();

            var sdk = new KubernetesSdk(kubernetesFactory.Object, logger);

            // Act
            var result = await sdk.GetSecretAsync(secretName, @namespace);
 
            // Assert
            kubernetes.Verify();
            Assert.Null(result);
        }

        [Fact]
        public async Task CreateSecretAsync_ShouldCreateWithExpectedValues()
        {
            // Arrange
            var secret = new V1Secret();
            var @namespace = "namespace";

            var kubernetes = new Mock<IKubernetes>(MockBehavior.Strict);
            kubernetes.Setup(k => k.CreateNamespacedSecretWithHttpMessagesAsync(secret, @namespace, null, null, null, null, CancellationToken.None))
                .ReturnsAsync(new HttpOperationResponse<V1Secret>())
                .Verifiable();
            
            var kubernetesFactory = new Mock<IKubernetesFactory>();
            kubernetesFactory
                .Setup(factory => factory.Create())
                .Returns(kubernetes.Object);

            var logger = new FakeLogger<KubernetesSdk>();

            var sdk = new KubernetesSdk(kubernetesFactory.Object, logger);

            // Act
            await sdk.CreateSecretAsync(secret, @namespace);
 
            // Assert
            kubernetes.Verify();
        }
        
        [Fact]
        public async Task CreateSecretAsync_ShouldLogAndRethrow_WhenException()
        {
            // Arrange
            var secretName = "secret123";
            var secret = new V1Secret {Metadata = new V1ObjectMeta {Name = secretName}};
            var @namespace = "namespace456";

            var originalException = new HttpOperationException();
            var kubernetes = new Mock<IKubernetes>(MockBehavior.Strict);
            kubernetes.Setup(k => k.CreateNamespacedSecretWithHttpMessagesAsync(secret, @namespace, null, null, null, null, CancellationToken.None))
                .Throws(originalException);
            
            var kubernetesFactory = new Mock<IKubernetesFactory>();
            kubernetesFactory
                .Setup(factory => factory.Create())
                .Returns(kubernetes.Object);

            var logger = new FakeLogger<KubernetesSdk>();

            var sdk = new KubernetesSdk(kubernetesFactory.Object, logger);

            // Act + first assert
            var result = await Assert.ThrowsAsync<HttpOperationException>(() => sdk.CreateSecretAsync(secret, @namespace));
 
            // Assert
            Assert.Same(originalException, result);
            Assert.Single(logger.Messages);
            Assert.Equal(LogLevel.Error, logger.Messages.Single().level);
            Assert.Contains(secretName, logger.Messages.Single().message);
            Assert.Contains(@namespace, logger.Messages.Single().message);
        }
        
        [Fact]
        public async Task ReplaceSecretAsync_ShouldReplaceWithExpectedValues()
        {
            // Arrange
            var secretName = "secret123";
            var secret = new V1Secret {Metadata = new V1ObjectMeta {Name = secretName}};
            var @namespace = "namespace";

            var kubernetes = new Mock<IKubernetes>(MockBehavior.Strict);
            kubernetes.Setup(k => k.ReplaceNamespacedSecretWithHttpMessagesAsync(secret, secretName, @namespace, null, null, null, null, CancellationToken.None))
                .ReturnsAsync(new HttpOperationResponse<V1Secret>())
                .Verifiable();
            
            var kubernetesFactory = new Mock<IKubernetesFactory>();
            kubernetesFactory
                .Setup(factory => factory.Create())
                .Returns(kubernetes.Object);

            var logger = new FakeLogger<KubernetesSdk>();

            var sdk = new KubernetesSdk(kubernetesFactory.Object, logger);

            // Act
            await sdk.ReplaceSecretAsync(secret, secretName, @namespace);
 
            // Assert
            kubernetes.Verify();
        }
        
        [Fact]
        public async Task ReplaceSecretAsync_ShouldLogAndRethrow_WhenException()
        {
            // Arrange
            var secretName = "secret123";
            var secret = new V1Secret {Metadata = new V1ObjectMeta {Name = secretName}};
            var @namespace = "namespace456";

            var originalException = new HttpOperationException();
            var kubernetes = new Mock<IKubernetes>(MockBehavior.Strict);
            kubernetes.Setup(k => k.ReplaceNamespacedSecretWithHttpMessagesAsync(secret, secretName, @namespace, null, null, null, null, CancellationToken.None))
                .Throws(originalException);
            
            var kubernetesFactory = new Mock<IKubernetesFactory>();
            kubernetesFactory
                .Setup(factory => factory.Create())
                .Returns(kubernetes.Object);

            var logger = new FakeLogger<KubernetesSdk>();

            var sdk = new KubernetesSdk(kubernetesFactory.Object, logger);

            // Act + first assert
            var result = await Assert.ThrowsAsync<HttpOperationException>(() => sdk.ReplaceSecretAsync(secret, secretName, @namespace));
 
            // Assert
            Assert.Same(originalException, result);
            Assert.Single(logger.Messages);
            Assert.Equal(LogLevel.Error, logger.Messages.Single().level);
            Assert.Contains(secretName, logger.Messages.Single().message);
            Assert.Contains(@namespace, logger.Messages.Single().message);
        }
        
        [Fact]
        public async Task DeleteSecretAsync_ShouldDeleteWithExpectedValues()
        {
            // Arrange
            var secretName = "secret123";
            var @namespace = "namespace";

            var kubernetes = new Mock<IKubernetes>(MockBehavior.Strict);
            kubernetes.Setup(k => k.DeleteNamespacedSecretWithHttpMessagesAsync(secretName, @namespace, null, null, null, null, null, null, null, CancellationToken.None))
                .ReturnsAsync(new HttpOperationResponse<V1Status>())
                .Verifiable();
            
            var kubernetesFactory = new Mock<IKubernetesFactory>();
            kubernetesFactory
                .Setup(factory => factory.Create())
                .Returns(kubernetes.Object);

            var logger = new FakeLogger<KubernetesSdk>();

            var sdk = new KubernetesSdk(kubernetesFactory.Object, logger);

            // Act
            await sdk.DeleteSecretAsync(secretName, @namespace);
 
            // Assert
            kubernetes.Verify();
        }
        
        [Fact]
        public async Task DeleteSecretAsync_ShouldLogAndRethrow_WhenException()
        {
            // Arrange
            var secretName = "secret123";
            var @namespace = "namespace456";

            var originalException = new HttpOperationException();
            var kubernetes = new Mock<IKubernetes>(MockBehavior.Strict);
            kubernetes.Setup(k => k.DeleteNamespacedSecretWithHttpMessagesAsync(secretName, @namespace, null, null, null, null, null, null, null, CancellationToken.None))
                .Throws(originalException);
            
            var kubernetesFactory = new Mock<IKubernetesFactory>();
            kubernetesFactory
                .Setup(factory => factory.Create())
                .Returns(kubernetes.Object);

            var logger = new FakeLogger<KubernetesSdk>();

            var sdk = new KubernetesSdk(kubernetesFactory.Object, logger);

            // Act + first assert
            var result = await Assert.ThrowsAsync<HttpOperationException>(() => sdk.DeleteSecretAsync(secretName, @namespace));
 
            // Assert
            Assert.Same(originalException, result);
            Assert.Single(logger.Messages);
            Assert.Equal(LogLevel.Error, logger.Messages.Single().level);
            Assert.Contains(secretName, logger.Messages.Single().message);
            Assert.Contains(@namespace, logger.Messages.Single().message);
        }
        
        [Fact]
        public async Task RestartDeploymentAsync_ShouldPatchDeploymentCorrectly()
        {
            // Arrange
            var deployment = "deployment123";
            var @namespace = "namespace";

            var kubernetes = new Mock<IKubernetes>(MockBehavior.Strict);
            kubernetes.Setup(k => k.ReadNamespacedDeploymentWithHttpMessagesAsync(deployment, @namespace, null, null, CancellationToken.None))
                .ReturnsAsync(new HttpOperationResponse<V1Deployment> {Body = new V1Deployment { Metadata = new V1ObjectMeta {Annotations = new Dictionary<string, string>()}}})
                .Verifiable();
            
            kubernetes.Setup(k => k.PatchNamespacedDeploymentWithHttpMessagesAsync(It.Is<V1Patch>(patch => IsExpectedPatch(patch)), deployment, @namespace, null, null, null, null, null, CancellationToken.None))
                .ReturnsAsync(new HttpOperationResponse<V1Deployment>())
                .Verifiable();
            
            var kubernetesFactory = new Mock<IKubernetesFactory>();
            kubernetesFactory
                .Setup(factory => factory.Create())
                .Returns(kubernetes.Object);

            var logger = new FakeLogger<KubernetesSdk>();

            var sdk = new KubernetesSdk(kubernetesFactory.Object, logger);

            // Act
            await sdk.RestartDeploymentAsync(deployment, @namespace);
 
            // Assert
            kubernetes.Verify();
        }
        
        private static bool IsExpectedPatch(V1Patch patch)
        {
            var jsonPatchDocument = ((JsonPatchDocument<V1Deployment>) patch.Content);
            var operationKey = ((Dictionary<string, string>) jsonPatchDocument.Operations.Single().value).Keys.Single();
            var operationType = jsonPatchDocument.Operations.Single().OperationType; 
            
            return patch.Type == V1Patch.PatchType.JsonPatch &&
                    operationKey == "kubectl.kubernetes.io/restartedAt" &&
                    operationType == OperationType.Replace;
        }
    }
}