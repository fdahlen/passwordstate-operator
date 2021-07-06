using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using PasswordstateOperator.Passwordstate;
using PasswordstateOperator.Rest;
using RestSharp;
using Xunit;

namespace PasswordstateOperator.Tests.Passwordstate
{
    public class PasswordstateSdkTests
    {
        [Fact]
        public async Task GetPasswordList_ShouldPerformCorrectRequest()
        {
            // Arrange
            var serverBaseUrl = "https://passwordstate.test.com";
            var passwordListId = "1000";
            var apiKey = "jkdsjh3j25234q!kj";

            var restResponse = new Mock<IRestResponse>(MockBehavior.Strict);
            restResponse
                .SetupGet(response => response.IsSuccessful)
                .Returns(true);
            restResponse
                .SetupGet(response => response.Content)
                .Returns("[]");
            
            var restClient = new Mock<IRestClient>(MockBehavior.Strict);
            restClient
                .Setup(client => client.ExecuteAsync(It.IsAny<IRestRequest>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(restResponse.Object));
            
            var restClientFactory = new Mock<IRestClientFactory>(MockBehavior.Strict);
            restClientFactory
                .Setup(factory => factory.New(serverBaseUrl))
                .Returns(restClient.Object);
            
            var sdk = new PasswordstateSdk(restClientFactory.Object);

            // Act
            await sdk.GetPasswordList(serverBaseUrl, passwordListId, apiKey);

            // Assert
            restClient.Verify(client => client.ExecuteAsync(
                    It.Is<IRestRequest>(request =>
                        request.Resource == $"/api/passwords/{passwordListId}" &&
                        request.Method == Method.GET &&
                        request.Parameters.Any(p => p.Type == ParameterType.HttpHeader && p.Name == "APIKey" && p.Value.ToString() == apiKey) &&
                        request.Parameters.Any(p => p.Type == ParameterType.QueryString && p.Name == "QueryAll")),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task GetPasswordList_ShouldReturnCorrectResult()
        {
            // Arrange
            var serverBaseUrl = "https://passwordstate.test.com";
            var passwordListId = "1000";
            var apiKey = "jkdsjh3j25234q!kj";

            var jsonResponse = "[\n\t{\n\t\t\"StringField1\": \"Value1\"\n\t}\n]";
            var expectedObjectResponse = new List<Password>
            {
                new()
                {
                    Fields = new List<Field>
                    {
                        new()
                        {
                            Name = "StringField1",
                            Value = "Value1"
                        },
                    }
                }
            };

            var restResponse = new Mock<IRestResponse>(MockBehavior.Strict);
            restResponse
                .SetupGet(response => response.IsSuccessful)
                .Returns(true);
            restResponse
                .SetupGet(response => response.Content)
                .Returns(jsonResponse);
            
            var restClient = new Mock<IRestClient>(MockBehavior.Strict);
            restClient
                .Setup(client => client.ExecuteAsync(It.IsAny<IRestRequest>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(restResponse.Object));
            
            var restClientFactory = new Mock<IRestClientFactory>(MockBehavior.Strict);
            restClientFactory
                .Setup(factory => factory.New(serverBaseUrl))
                .Returns(restClient.Object);
            
            var sdk = new PasswordstateSdk(restClientFactory.Object);

            // Act
            var result = await sdk.GetPasswordList(serverBaseUrl, passwordListId, apiKey);

            // Assert
            Assert.Equal(jsonResponse, result.Json);
            expectedObjectResponse.AssertEquals(result.Passwords);
        }

        [Fact]
        public async Task GetPasswordList_ShouldThrow_WhenNonSuccessfulResponse()
        {
            // Arrange
            var serverBaseUrl = "https://passwordstate.test.com";
            var passwordListId = "1000";
            var apiKey = "jkdsjh3j25234q!kj";

            var errorMessage = "Message stuff";
            var errorException = new FormatException("Invalid");
            
            var restResponse = new Mock<IRestResponse>(MockBehavior.Strict);
            restResponse
                .SetupGet(response => response.IsSuccessful)
                .Returns(false);
            restResponse
                .SetupGet(response => response.ErrorMessage)
                .Returns(errorMessage);
            restResponse
                .SetupGet(response => response.ErrorException)
                .Returns(errorException);
            
            var restClient = new Mock<IRestClient>(MockBehavior.Strict);
            restClient
                .Setup(client => client.ExecuteAsync(It.IsAny<IRestRequest>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(restResponse.Object));
            
            var restClientFactory = new Mock<IRestClientFactory>(MockBehavior.Strict);
            restClientFactory
                .Setup(factory => factory.New(serverBaseUrl))
                .Returns(restClient.Object);
            
            var sdk = new PasswordstateSdk(restClientFactory.Object);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ApplicationException>(async () => await sdk.GetPasswordList(serverBaseUrl, passwordListId, apiKey));
            
            // Assert
            Assert.Contains(passwordListId, exception.Message);
            Assert.Contains(errorMessage, exception.Message);
            Assert.Contains(errorException.ToString(), exception.Message);
        }
    }
}