using System.Threading.Tasks;

namespace PasswordstateOperator.Operations
{
    public interface ICreateOperation
    {
        Task Create(PasswordListCrd crd);
    }
}