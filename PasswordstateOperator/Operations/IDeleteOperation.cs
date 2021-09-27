using System.Threading.Tasks;

namespace PasswordstateOperator.Operations
{
    public interface IDeleteOperation
    {
        Task Delete(PasswordListCrd crd);
    }
}