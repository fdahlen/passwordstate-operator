using RestSharp;

namespace PasswordstateOperator.Rest
{
    public interface IRestClientFactory
    {
        IRestClient New(string baseUrl);
    }
}