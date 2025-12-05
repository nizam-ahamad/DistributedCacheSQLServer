using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace WebApplication1.Services
{
    public class SqlServerCacheService : ICacheService
    {
        private readonly IDistributedCache _cache;
        private readonly ILogger<SqlServerCacheService> _logger;
        private readonly int _defaultExpirationMinutes;

        public SqlServerCacheService(
            IDistributedCache cache,
            ILogger<SqlServerCacheService> logger,
            IConfiguration configuration)
        {
            _cache = cache;
            _logger = logger;
            _defaultExpirationMinutes = configuration.GetValue<int>("CacheSettings:DefaultExpirationMinutes", 10);
        }

        public async Task<T?> GetAsync<T>(string key) where T : class
        {
            try
            {
                var cachedData = await _cache.GetStringAsync(key);
                
                if (string.IsNullOrEmpty(cachedData))
                {
                    _logger.LogInformation("Cache miss for key: {Key}", key);
                    return null;
                }

                _logger.LogInformation("Cache hit for key: {Key}", key);
                return JsonSerializer.Deserialize<T>(cachedData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache for key: {Key}", key);
                return null;
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
        {
            try
            {
                var serializedData = JsonSerializer.Serialize(value);
                
                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromMinutes(_defaultExpirationMinutes),
                    SlidingExpiration = TimeSpan.FromMinutes(_defaultExpirationMinutes / 2)
                };

                await _cache.SetStringAsync(key, serializedData, options);
                _logger.LogInformation("Cache set for key: {Key} with expiration: {Expiration}", key, options.AbsoluteExpirationRelativeToNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting cache for key: {Key}", key);
            }
        }

        public async Task RemoveAsync(string key)
        {
            try
            {
                await _cache.RemoveAsync(key);
                _logger.LogInformation("Cache removed for key: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache for key: {Key}", key);
            }
        }

        public async Task<bool> ExistsAsync(string key)
        {
            try
            {
                var cachedData = await _cache.GetStringAsync(key);
                return !string.IsNullOrEmpty(cachedData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking cache existence for key: {Key}", key);
                return false;
            }
        }

        public async Task<T?> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null) where T : class
        {
            var cachedData = await GetAsync<T>(key);
            
            if (cachedData != null)
            {
                return cachedData;
            }

            _logger.LogInformation("Executing factory for key: {Key}", key);
            var data = await factory();
            
            if (data != null)
            {
                await SetAsync(key, data, expiration);
            }

            return data;
        }
    }
}