using System;
using System.Threading.Tasks;
using PasswordstateOperator.Rest;
using RestSharp;

namespace PasswordstateOperator.Passwordstate
{
    /// <summary>
    /// Supports Click Studios Passwordstate API version 8.9
    /// </summary>
    public class PasswordstateSdk : IPasswordstateSdk
    {
        private readonly IRestClientFactory restClientFactory;
        private readonly PasswordsParser passwordsParser;

        public PasswordstateSdk(IRestClientFactory restClientFactory, PasswordsParser passwordsParser)
        {
            this.restClientFactory = restClientFactory;
            this.passwordsParser = passwordsParser;
        }

        public async Task<PasswordListResponse> GetPasswordList(string serverBaseUrl, string passwordListId, string apiKey)
        {
            var restClient = restClientFactory.New(serverBaseUrl);
            var restRequest = new RestRequest($"/api/passwords/{passwordListId}", Method.GET);
            restRequest.AddHeader("APIKey", apiKey);
            restRequest.AddQueryParameter("QueryAll", "true");

            var result = await restClient.ExecuteAsync(restRequest);

            if (!result.IsSuccessful)
            {
                throw new ApplicationException($"Failed to fetch password list with id {passwordListId} from Passwordstate: {result.ErrorMessage} {result.ErrorException}");
            }

            return new PasswordListResponse
            {
                Json = result.Content,
                Passwords = passwordsParser.Parse(result.Content)
            };
        }
    }
}