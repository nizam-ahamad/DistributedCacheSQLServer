using WebApplication1.Models;

namespace WebApplication1.Repositories
{
    public interface IUserRepository
    {
        IEnumerable<User> GetUsers();
    }
}
