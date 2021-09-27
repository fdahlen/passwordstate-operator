using System.Threading.Tasks;

namespace PasswordstateOperator.Operations
{
    public interface IUpdateOperation
    {
        Task Update(PasswordListCrd existingCrd, PasswordListCrd newCrd);
    }
}