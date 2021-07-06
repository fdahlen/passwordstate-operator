using RestSharp;

namespace PasswordstateOperator.Rest
{
    public class RestClientFactory : IRestClientFactory
    {
        public IRestClient New(string baseUrl)
        {
            return new RestClient(baseUrl);
        }
    }
}