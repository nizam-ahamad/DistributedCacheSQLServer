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
            // Get data with source tracking
            var result = await _cacheService.GetOrSetWithSourceAsync(
                UsersCacheKey,
                async () =>
                {
                    _logger.LogInformation("Fetching users from repository (cache miss)");
                    var users = await _userRepository.GetUsersAsync();
                    return users.ToList();
                },
                TimeSpan.FromMinutes(5)
            );

            return Ok(new 
            { 
                data = result.Data,
                source = result.Source,
                isFromCache = result.IsFromCache,
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
                cacheType = "File-Based Cache",
                timestamp = DateTime.UtcNow
            });
        }

        [HttpPost("cache/cleanup")]
        public async Task<IActionResult> CleanupCache()
        {
            if (_cacheService is FileCacheService fileCacheService)
            {
                await fileCacheService.CleanupExpiredCacheAsync();
                return Ok(new { message = "Cache cleanup completed", timestamp = DateTime.UtcNow });
            }

            return BadRequest(new { message = "Cache service does not support cleanup" });
        }
    }
}
