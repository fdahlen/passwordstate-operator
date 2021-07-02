using System;
using System.Threading.Tasks;
using RestSharp;

namespace PasswordstateOperator.Passwordstate
{
    public class PasswordstateSdk
    {
        public async Task<IRestResponse> GetPasswordList(string serverBaseUrl, int passwordListId, string apiKey)
        {
            var restClient = new RestClient(serverBaseUrl);
            var restRequest = new RestRequest($"/api/passwords/{passwordListId}", Method.GET);
            restRequest.AddHeader("APIKey", apiKey);
            restRequest.AddQueryParameter("QueryAll", "true");

            var result = await restClient.ExecuteAsync(restRequest);

            if (!result.IsSuccessful)
            {
                throw new ApplicationException($"Failed to fetch password list with id {passwordListId} from Passwordstate: {result.ErrorMessage} {result.ErrorException}");
            }

            return result;
        }
    }
}