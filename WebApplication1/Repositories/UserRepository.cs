using WebApplication1.Models;

namespace WebApplication1.Repositories
{
    public class UserRepository : IUserRepository
    {
        public IEnumerable<User> GetUsers()
        {
            var user1 = new User { UserId = 1, UserName = "User1" };
            var user2 = new User { UserId = 2, UserName = "User2" };

            var users = new List<User>
            {
                user1,
                user2
            };

            return users;
        }
    }
}
