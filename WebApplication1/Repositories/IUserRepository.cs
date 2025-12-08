using WebApplication1.Models;
using System.Threading.Tasks;

namespace WebApplication1.Repositories
{
    public interface IUserRepository
    {
        Task<IEnumerable<User>> GetUsersAsync();
    }
}
