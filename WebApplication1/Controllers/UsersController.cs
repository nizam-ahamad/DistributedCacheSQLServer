using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Repositories;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly IUserRepository _userRepository;
        private readonly ICacheService _cacheService;
        private readonly ILogger<UsersController> _logger;
        private const string UsersCacheKey = "all_users";

        public UsersController(
            IUserRepository userRepository,
            ICacheService cacheService,
            ILogger<UsersController> logger)
        {
            _userRepository = userRepository;
            _cacheService = cacheService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            // Try to get from cache, if not found, fetch from repository and cache it
            var users = await _cacheService.GetOrSetAsync(
                UsersCacheKey,
                async () =>
                {
                    _logger.LogInformation("Fetching users from repository (cache miss)");
                    return _userRepository.GetUsers().ToList();
                },
                TimeSpan.FromMinutes(5)
            );

            return Ok(new 
            { 
                data = users,
                source = users != null ? "cache or repository" : "none",
                timestamp = DateTime.UtcNow
            });
        }

        [HttpDelete("cache")]
        public async Task<IActionResult> ClearCache()
        {
            await _cacheService.RemoveAsync(UsersCacheKey);
            return Ok(new { message = "Cache cleared successfully", timestamp = DateTime.UtcNow });
        }

        [HttpGet("cache/status")]
        public async Task<IActionResult> GetCacheStatus()
        {
            var exists = await _cacheService.ExistsAsync(UsersCacheKey);
            return Ok(new 
            { 
                cacheKey = UsersCacheKey, 
                exists,
                cacheType = "SQL Server Distributed Cache",
                timestamp = DateTime.UtcNow
            });
        }
    }
}
