using System.Threading.Tasks;

namespace PasswordstateOperator.Passwordstate
{
    public interface IPasswordstateSdk
    {
        Task<PasswordListResponse> GetPasswordList(string serverBaseUrl, string passwordListId, string apiKey);
    }
}