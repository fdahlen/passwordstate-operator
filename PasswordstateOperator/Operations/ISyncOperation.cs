using System.Threading.Tasks;
using k8s.Models;

namespace PasswordstateOperator.Operations
{
    public interface ISyncOperation
    {
        Task Sync(PasswordListCrd crd, V1Secret existingPasswordsSecret);
    }
}