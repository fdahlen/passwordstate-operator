using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using RestSharp;

namespace PasswordstateOperator.Passwordstate
{
    public class PasswordstateSdk
    {
        public async Task<PasswordListResponse> GetPasswordList(string serverBaseUrl, string passwordListId, string apiKey)
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

            return new PasswordListResponse
            {
                Json = result.Content,
                Passwords = ParsePasswords(result.Content)
            };
        }
        
        private static List<Password> ParsePasswords(string json)
        {
            return JsonSerializer.Deserialize<List<Dictionary<string, dynamic>>>(json)
                .Select(password => new Password
                {
                    Fields = password.Select(field => new Field
                    {
                        Name = field.Key,
                        Value = field.Value.ToString()
                    }).ToList()
                })
                .ToList();
        }
    }
}