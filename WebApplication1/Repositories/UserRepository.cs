using WebApplication1.Models;

namespace WebApplication1.Repositories
{
    public class UserRepository : IUserRepository
    {
        public async Task<IEnumerable<User>> GetUsersAsync()
        {
            // Simulate async database call
            await Task.Delay(100); // Simulates I/O delay (e.g., database query time)

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
