using System.Threading.Tasks;
using k8s.Models;

namespace PasswordstateOperator.Operations
{
    public interface IGetOperation
    {
        Task<V1Secret> Get(PasswordListCrd crd);
    }
}